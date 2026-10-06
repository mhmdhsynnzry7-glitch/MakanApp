using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task MessageEditDeleteReactionAndPinProduceOrderedDurableChanges()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var secondDevice = factory.CreateClient();
        UseBearerToken(secondDevice, setup.First.AccessToken);
        var baseline = await GetChangesAsync(setup.FirstClient, setup.ConversationId);
        var sent = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "sync source");
        var afterSend = await GetChangesAsync(
            setup.FirstClient,
            setup.ConversationId,
            baseline.NextCursor);
        var created = Assert.Single(afterSend.Changes);
        Assert.Equal(MessagingChangeType.MessageCreated, created.Type);
        Assert.Equal(sent.MessageId, created.ResourceId);

        using var editResponse = await secondDevice.PatchAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}",
            new EditMessageCommand("edited on device b", sent.Version),
            JsonOptions);
        editResponse.EnsureSuccessStatusCode();
        var edited = (await editResponse.Content.ReadFromJsonAsync<MessageMutationResult>(JsonOptions))!;
        using var reactionResponse = await setup.SecondClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}/reactions",
            new AddReactionCommand(MessageReactionType.Love),
            JsonOptions);
        reactionResponse.EnsureSuccessStatusCode();
        using var pinResponse = await setup.FirstClient.PostAsync(
            $"/api/v1/conversations/{setup.ConversationId}/pins/{sent.MessageId}",
            null);
        pinResponse.EnsureSuccessStatusCode();
        using var deleteResponse = await secondDevice.DeleteAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}?expectedVersion={Uri.EscapeDataString(edited.Version)}");
        deleteResponse.EnsureSuccessStatusCode();

        var recovered = await GetChangesAsync(
            setup.FirstClient,
            setup.ConversationId,
            afterSend.NextCursor,
            10);
        Assert.Equal(
            new[]
            {
                MessagingChangeType.MessageEdited,
                MessagingChangeType.ReactionChanged,
                MessagingChangeType.PinChanged,
                MessagingChangeType.MessageDeleted
            },
            recovered.Changes.Select(change => change.Type));
        Assert.All(recovered.Changes, change => Assert.Equal(sent.MessageId, change.ResourceId));
        Assert.Equal(sent.Sequence, (await factory.GetAdvancedMessageAsync(sent.MessageId)).Sequence);
        Assert.Equal(
            recovered.Changes.Count,
            recovered.Changes.Select(change => change.Cursor).Distinct().Count());
    }

    [Fact]
    public async Task DeltaPaginationHasNoSkipsOrDuplicatesAndRejectsForeignCursor()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var baseline = await GetChangesAsync(setup.FirstClient, setup.ConversationId);
        for (var index = 0; index < 5; index++)
        {
            await SendMessageAndReadAsync(
                setup.FirstClient,
                setup.ConversationId,
                Guid.NewGuid(),
                $"page-{index}");
        }

        var cursor = baseline.NextCursor;
        var all = new List<ConversationChangeResult>();
        while (true)
        {
            var page = await GetChangesAsync(setup.FirstClient, setup.ConversationId, cursor, 2);
            all.AddRange(page.Changes);
            cursor = page.NextCursor;
            if (!page.HasMore)
            {
                break;
            }
        }

        Assert.Equal(5, all.Count);
        Assert.Equal(5, all.Select(change => change.ChangeId).Distinct().Count());
        Assert.All(all, change => Assert.Equal(MessagingChangeType.MessageCreated, change.Type));

        using var otherSetup = await CreateAdvancedConversationSetupAsync();
        using var invalid = await setup.FirstClient.GetAsync(
            $"/api/v1/conversations/{setup.ConversationId}/changes?afterCursor={Uri.EscapeDataString((await GetChangesAsync(otherSetup.FirstClient, otherSetup.ConversationId)).NextCursor)}");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(MessagingErrorCodes.ChangeCursorInvalid, await ReadProblemCodeAsync(invalid));
    }

    [Fact]
    public async Task RemovedParticipantCannotUseOldCursorAndRemovalCreatesDelta()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("delta revocation", member.UserId));
        var memberBaseline = await GetChangesAsync(memberClient, group.ConversationId);
        var ownerBaseline = await GetChangesAsync(ownerClient, group.ConversationId);

        using var remove = await ownerClient.DeleteAsync(
            $"/api/v1/conversations/{group.ConversationId}/members/{member.UserId}");
        remove.EnsureSuccessStatusCode();
        using var revokedDelta = await memberClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/changes?afterCursor={Uri.EscapeDataString(memberBaseline.NextCursor)}");
        var ownerDelta = await GetChangesAsync(
            ownerClient,
            group.ConversationId,
            ownerBaseline.NextCursor);

        Assert.Equal(HttpStatusCode.NotFound, revokedDelta.StatusCode);
        Assert.Contains(ownerDelta.Changes, change =>
            change.Type == MessagingChangeType.ParticipantChanged);

        await using var revokedConnection = CreateHubConnection(member.AccessToken);
        await revokedConnection.StartAsync();
        var hubException = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
            revokedConnection.InvokeAsync("SubscribeConversation", group.ConversationId));
        Assert.Contains(
            MessagingErrorCodes.RealtimeSubscriptionNotAllowed,
            hubException.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAndDeliveryCursorsAreMonotonicAndUnreadUsesDurableReadCursor()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        for (var index = 0; index < 3; index++)
        {
            await SendMessageAndReadAsync(
                setup.FirstClient,
                setup.ConversationId,
                Guid.NewGuid(),
                $"unread-{index}");
        }

        var firstBaseline = await GetChangesAsync(setup.FirstClient, setup.ConversationId);
        var secondBaseline = await GetChangesAsync(setup.SecondClient, setup.ConversationId);
        var summaries = await setup.SecondClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations",
            JsonOptions);
        Assert.Equal(3, Assert.Single(summaries!).UnreadCount);

        var delivered = await AdvanceCursorAsync(
            setup.SecondClient,
            setup.ConversationId,
            "delivered",
            2);
        var backwardDelivery = await AdvanceCursorAsync(
            setup.SecondClient,
            setup.ConversationId,
            "delivered",
            1);
        var read = await AdvanceCursorAsync(
            setup.SecondClient,
            setup.ConversationId,
            "read",
            2);
        var backward = await AdvanceCursorAsync(
            setup.SecondClient,
            setup.ConversationId,
            "read",
            1);
        var completed = await AdvanceCursorAsync(
            setup.SecondClient,
            setup.ConversationId,
            "read",
            3);

        Assert.Equal(2, delivered.LastDeliveredMessageSequence);
        Assert.Equal(0, delivered.LastReadMessageSequence);
        Assert.Equal(2, backwardDelivery.LastDeliveredMessageSequence);
        Assert.Equal(0, backwardDelivery.LastReadMessageSequence);
        Assert.Equal(2, read.LastReadMessageSequence);
        Assert.Equal(1, read.UnreadCount);
        Assert.Equal(2, backward.LastReadMessageSequence);
        Assert.Equal(3, completed.LastDeliveredMessageSequence);
        Assert.Equal(3, completed.LastReadMessageSequence);
        Assert.Equal(0, completed.UnreadCount);

        var secondDelta = await GetChangesAsync(
            setup.SecondClient,
            setup.ConversationId,
            secondBaseline.NextCursor,
            10);
        var firstDelta = await GetChangesAsync(
            setup.FirstClient,
            setup.ConversationId,
            firstBaseline.NextCursor,
            10);
        Assert.Contains(secondDelta.Changes, change =>
            change.Type == MessagingChangeType.DeliveryCursorAdvanced);
        Assert.Contains(secondDelta.Changes, change =>
            change.Type == MessagingChangeType.ReadCursorAdvanced);
        Assert.DoesNotContain(firstDelta.Changes, change =>
            change.Type is MessagingChangeType.DeliveryCursorAdvanced or MessagingChangeType.ReadCursorAdvanced);
    }

    [Fact]
    public async Task ConcurrentReadAdvancesPersistMaximumSequence()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        await factory.SeedMessagesForCursorConcurrencyAsync(
            setup.ConversationId,
            setup.First.UserId,
            100);
        await AdvanceCursorAsync(setup.SecondClient, setup.ConversationId, "read", 10);
        var clients = Enumerable.Range(0, 3).Select(_ => factory.CreateClient()).ToArray();
        foreach (var client in clients)
        {
            UseBearerToken(client, setup.Second.AccessToken);
        }

        try
        {
            var targets = new[] { 100L, 50L, 75L };
            var responses = await Task.WhenAll(clients.Select((client, index) =>
                client.PostAsJsonAsync(
                    $"/api/v1/conversations/{setup.ConversationId}/read",
                    new AdvanceConversationCursorCommand(targets[index]),
                    JsonOptions)));
            try
            {
                Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }

        var cursor = await factory.GetParticipantCursorAsync(
            setup.ConversationId,
            setup.Second.UserId);
        Assert.Equal(100, cursor.LastReadMessageSequence);
        Assert.Equal(100, cursor.LastDeliveredMessageSequence);
    }

    [Fact]
    public async Task OfflineRetryCreatesOneMessageAndOneChangeEffect()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var baseline = await GetChangesAsync(setup.FirstClient, setup.ConversationId);
        var clientMessageId = Guid.NewGuid();
        var first = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            clientMessageId,
            "offline retry");
        var retry = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            clientMessageId,
            "offline retry");
        var delta = await GetChangesAsync(
            setup.FirstClient,
            setup.ConversationId,
            baseline.NextCursor);

        Assert.Equal(first.MessageId, retry.MessageId);
        Assert.Equal(first.Sequence, retry.Sequence);
        Assert.Single(await factory.GetMessagesAsync(setup.ConversationId));
        Assert.Single(delta.Changes, change =>
            change.Type == MessagingChangeType.MessageCreated &&
            change.ResourceId == first.MessageId);
    }

    [Fact]
    public async Task MissedRealtimeEventIsRecoverableFromDurableDelta()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var deviceBBaseline = await GetChangesAsync(
            setup.SecondClient,
            setup.ConversationId);

        var sent = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "recover without realtime");

        var recovered = await GetChangesAsync(
            setup.SecondClient,
            setup.ConversationId,
            deviceBBaseline.NextCursor);
        var created = Assert.Single(
            recovered.Changes,
            change => change.Type == MessagingChangeType.MessageCreated);

        Assert.Equal(sent.MessageId, created.ResourceId);
    }

    private static async Task<ConversationChangePageResult> GetChangesAsync(
        HttpClient client,
        Guid conversationId,
        string? afterCursor = null,
        int? limit = null)
    {
        var query = new List<string>();
        if (afterCursor is not null)
        {
            query.Add($"afterCursor={Uri.EscapeDataString(afterCursor)}");
        }

        if (limit.HasValue)
        {
            query.Add($"limit={limit.Value}");
        }

        var suffix = query.Count == 0 ? string.Empty : $"?{string.Join('&', query)}";
        using var response = await client.GetAsync(
            $"/api/v1/conversations/{conversationId}/changes{suffix}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationChangePageResult>(JsonOptions))!;
    }

    private static async Task<ConversationCursorStateResult> AdvanceCursorAsync(
        HttpClient client,
        Guid conversationId,
        string cursorType,
        long sequence)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/{cursorType}",
            new AdvanceConversationCursorCommand(sequence),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ConversationCursorStateResult>(JsonOptions))!;
    }
}
