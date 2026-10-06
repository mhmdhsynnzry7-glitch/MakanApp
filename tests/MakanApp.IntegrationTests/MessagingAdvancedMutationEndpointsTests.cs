using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Domain.Messaging;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task TextEditPersistsRevisionPreservesIdentityAndDoesNotExposeOldText()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var sent = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "secret old text");

        using var editResponse = await setup.FirstClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}",
            new EditMessageCommand("new text", sent.Version),
            JsonOptions);
        editResponse.EnsureSuccessStatusCode();
        var edited = await editResponse.Content.ReadFromJsonAsync<MessageMutationResult>(JsonOptions);
        using var historyResponse = await setup.SecondClient.GetAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages");
        var historyJson = await historyResponse.Content.ReadAsStringAsync();
        var history = System.Text.Json.JsonSerializer.Deserialize<ConversationMessagePageResult>(
            historyJson,
            JsonOptions)!;
        var current = Assert.Single(history.Messages);
        var stored = await factory.GetAdvancedMessageAsync(sent.MessageId);
        var revisions = await factory.GetMessageRevisionTextsAsync(sent.MessageId);

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.Equal(sent.MessageId, current.MessageId);
        Assert.Equal(sent.Sequence, current.Sequence);
        Assert.Equal(MessageKind.Text, current.Kind);
        Assert.Equal("new text", current.Text);
        Assert.True(current.IsEdited);
        Assert.False(current.IsDeleted);
        Assert.NotEqual(sent.Version, edited!.Version);
        Assert.Equal(2, stored.CurrentRevisionNumber);
        Assert.Equal(new[] { "secret old text", "new text" }, revisions);
        Assert.DoesNotContain("secret old text", historyJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaleEditAndEditByAnotherParticipantAreRejected()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var sent = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "original");
        using var accepted = await setup.FirstClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}",
            new EditMessageCommand("first edit", sent.Version),
            JsonOptions);
        using var stale = await setup.FirstClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}",
            new EditMessageCommand("stale edit", sent.Version),
            JsonOptions);
        var current = await accepted.Content.ReadFromJsonAsync<MessageMutationResult>(JsonOptions);
        using var otherUser = await setup.SecondClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}",
            new EditMessageCommand("not mine", current!.Version),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageEditConflict, await ReadProblemCodeAsync(stale));
        Assert.Equal(HttpStatusCode.Conflict, otherUser.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageNotEditable, await ReadProblemCodeAsync(otherUser));
    }

    [Fact]
    public async Task SenderDeleteProducesSafeTombstoneAndDeletedMessageCannotBeEdited()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var sent = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "remove this secret");
        using var deleteResponse = await setup.FirstClient.DeleteAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}?expectedVersion={Uri.EscapeDataString(sent.Version)}");
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<MessageMutationResult>(JsonOptions);
        var history = await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages",
            JsonOptions);
        var tombstone = Assert.Single(history!.Messages);
        using var editDeleted = await setup.FirstClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}",
            new EditMessageCommand("resurrect", deleted!.Version),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.True(tombstone.IsDeleted);
        Assert.Null(tombstone.Kind);
        Assert.Null(tombstone.Text);
        Assert.Empty(tombstone.Attachments);
        Assert.NotNull(tombstone.DeletedAtUtc);
        Assert.Equal(sent.Sequence, tombstone.Sequence);
        Assert.Equal(HttpStatusCode.Conflict, editDeleted.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageDeleted, await ReadProblemCodeAsync(editDeleted));
    }

    [Fact]
    public async Task ReplyWithinConversationWorksAndDeletedSourceUsesSafePlaceholder()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var source = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "quoted private text");
        var reply = await SendAdvancedMessageAsync(
            setup.SecondClient,
            setup.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Text = "reply",
                ReplyToMessageId = source.MessageId
            });
        using var delete = await setup.FirstClient.DeleteAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{source.MessageId}?expectedVersion={Uri.EscapeDataString(source.Version)}");
        delete.EnsureSuccessStatusCode();
        var history = await setup.SecondClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/messages",
            JsonOptions);
        var replyResult = history!.Messages.Single(item => item.MessageId == reply.MessageId);

        Assert.NotNull(replyResult.Reply);
        Assert.True(replyResult.Reply!.IsDeleted);
        Assert.Null(replyResult.Reply.Text);
        Assert.Null(replyResult.Reply.Kind);
    }

    [Fact]
    public async Task ReplyToMessageInAnotherConversationIsRejected()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var thirdClient = factory.CreateClient();
        var third = await CreateMessagingUserAsync(thirdClient);
        await factory.CreatePersonalCommunicationGrantAsync(setup.First.UserId, third.UserId);
        var otherConversation = await StartDirectAndReadAsync(
            setup.FirstClient,
            third.UserId,
            ConversationScope.Personal);
        var source = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "source");
        using var response = await setup.FirstClient.PostAsJsonAsync(
            $"/api/v1/conversations/{otherConversation.ConversationId}/messages",
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Text = "invalid reply",
                ReplyToMessageId = source.MessageId
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageReplyNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ForwardCreatesNewMessageAndUnauthorizedSourceIsConcealed()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var thirdClient = factory.CreateClient();
        var third = await CreateMessagingUserAsync(thirdClient);
        await factory.CreatePersonalCommunicationGrantAsync(setup.First.UserId, third.UserId);
        var destination = await StartDirectAndReadAsync(
            setup.FirstClient,
            third.UserId,
            ConversationScope.Personal);
        var source = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "forward body");
        using var forwardResponse = await setup.FirstClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{source.MessageId}/forward",
            new ForwardMessageCommand(destination.ConversationId, Guid.NewGuid()),
            JsonOptions);
        var forwarded = await forwardResponse.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions);
        using var unauthorized = await thirdClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{source.MessageId}/forward",
            new ForwardMessageCommand(destination.ConversationId, Guid.NewGuid()),
            JsonOptions);
        var stored = await factory.GetAdvancedMessageAsync(forwarded!.MessageId);

        Assert.Equal(HttpStatusCode.OK, forwardResponse.StatusCode);
        Assert.NotEqual(source.MessageId, forwarded.MessageId);
        Assert.Equal(destination.ConversationId, forwarded.ConversationId);
        Assert.Equal(source.MessageId, stored.ForwardedFromMessageId);
        Assert.Equal(HttpStatusCode.NotFound, unauthorized.StatusCode);
    }

    [Fact]
    public async Task ConcurrentEditDeleteAllowsOneWinnerWithoutResurrection()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var sent = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "race source");
        var editTask = setup.FirstClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}",
            new EditMessageCommand("race edit", sent.Version),
            JsonOptions);
        var deleteTask = setup.FirstClient.DeleteAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{sent.MessageId}?expectedVersion={Uri.EscapeDataString(sent.Version)}");
        using var edit = await editTask;
        using var delete = await deleteTask;
        var statuses = new[] { edit.StatusCode, delete.StatusCode };
        var stored = await factory.GetAdvancedMessageAsync(sent.MessageId);

        Assert.Single(statuses, status => status == HttpStatusCode.OK);
        Assert.Single(statuses, status => status == HttpStatusCode.PreconditionFailed);
        if (stored.DeletedAtUtc.HasValue)
        {
            Assert.Null(stored.Text);
        }
        else
        {
            Assert.Equal("race edit", stored.Text);
        }
    }

    [Fact]
    public async Task UnauthenticatedMessageMutationIsRejected()
    {
        using var client = factory.CreateClient();
        using var response = await client.PatchAsJsonAsync(
            $"/api/v1/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}",
            new EditMessageCommand("no auth", Convert.ToBase64String(new byte[8])),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<AdvancedConversationSetup> CreateAdvancedConversationSetupAsync()
    {
        var firstClient = factory.CreateClient();
        var secondClient = factory.CreateClient();
        try
        {
            var first = await CreateMessagingUserAsync(firstClient);
            var second = await CreateMessagingUserAsync(secondClient);
            await factory.CreatePersonalCommunicationGrantAsync(first.UserId, second.UserId);
            var conversation = await StartDirectAndReadAsync(
                firstClient,
                second.UserId,
                ConversationScope.Personal);
            return new AdvancedConversationSetup(
                firstClient,
                secondClient,
                first,
                second,
                conversation.ConversationId);
        }
        catch
        {
            firstClient.Dispose();
            secondClient.Dispose();
            throw;
        }
    }

    private static async Task<MessageReceiptResult> SendAdvancedMessageAsync(
        HttpClient client,
        Guid conversationId,
        SendMessageCommand command)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            command,
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!;
    }

    private sealed record AdvancedConversationSetup(
        HttpClient FirstClient,
        HttpClient SecondClient,
        MessagingUser First,
        MessagingUser Second,
        Guid ConversationId) : IDisposable
    {
        public void Dispose()
        {
            FirstClient.Dispose();
            SecondClient.Dispose();
        }
    }
}
