using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Serialization;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task AuthorizedParticipantReceivesInvalidationAndRecoversDurablyFromDelta()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var baseline = await GetChangesAsync(setup.SecondClient, setup.ConversationId);
        await using var connection = CreateHubConnection(setup.Second.AccessToken);
        var notificationReceived = new TaskCompletionSource<MessagingRealtimeNotification>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<MessagingRealtimeNotification>(
            MessagingRealtimeEventNames.ConversationChanged,
            notification =>
            {
                if (notification.ConversationId == setup.ConversationId &&
                    notification.Type == MakanApp.Domain.Messaging.MessagingChangeType.MessageCreated)
                {
                    notificationReceived.TrySetResult(notification);
                }
            });
        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeConversation", setup.ConversationId);

        var sent = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "realtime invalidation");
        var notification = await notificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var recovered = await GetChangesAsync(
            setup.SecondClient,
            setup.ConversationId,
            baseline.NextCursor);

        Assert.Equal(sent.MessageId, notification.ResourceId);
        Assert.Equal(setup.ConversationId, notification.ConversationId);
        Assert.Equal(
            sent.MessageId,
            Assert.Single(
                recovered.Changes,
                change => change.Type == MakanApp.Domain.Messaging.MessagingChangeType.MessageCreated)
                .ResourceId);
    }

    [Fact]
    public async Task HubSubscriptionRechecksConversationAuthorization()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var outsiderClient = factory.CreateClient();
        var outsider = await CreateMessagingUserAsync(outsiderClient);
        await using var outsiderConnection = CreateHubConnection(outsider.AccessToken);
        await outsiderConnection.StartAsync();

        var exception = await Assert.ThrowsAsync<HubException>(() =>
            outsiderConnection.InvokeAsync("SubscribeConversation", setup.ConversationId));

        Assert.Contains(
            MessagingErrorCodes.RealtimeSubscriptionNotAllowed,
            exception.Message,
            StringComparison.Ordinal);

        await using var participantConnection = CreateHubConnection(setup.Second.AccessToken);
        await participantConnection.StartAsync();
        await participantConnection.InvokeAsync("SubscribeConversation", setup.ConversationId);
    }

    [Fact]
    public async Task RevokedSessionCannotContinueRealtimeSubscription()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        await using var connection = CreateHubConnection(setup.First.AccessToken);
        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeConversation", setup.ConversationId);

        using var logout = await setup.FirstClient.PostAsync("/api/v1/auth/logout", null);
        logout.EnsureSuccessStatusCode();

        var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
            connection.InvokeAsync("SubscribeConversation", setup.ConversationId));

        Assert.True(
            exception is HubException or HttpRequestException,
            $"Unexpected exception type: {exception.GetType().FullName}");
        Assert.True(
            exception.Message.Contains(IdentityErrorCodes.AuthRequired, StringComparison.Ordinal) ||
            exception.Message.Contains("401", StringComparison.Ordinal),
            $"Unexpected revocation error: {exception.Message}");
    }

    [Fact]
    public async Task RealtimeDispatchFailureDoesNotRollbackCommittedMessageOrChange()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var failureSwitch = factory.Services.GetRequiredService<RealtimeFailureSwitch>();
        failureSwitch.Reset();
        failureSwitch.FailDispatch = true;
        try
        {
            var sent = await SendMessageAndReadAsync(
                setup.FirstClient,
                setup.ConversationId,
                Guid.NewGuid(),
                "committed before realtime");
            await failureSwitch.FailureObserved.WaitAsync(TimeSpan.FromSeconds(10));

            var messages = await factory.GetMessagesAsync(setup.ConversationId);
            var changes = await factory.GetMessagingChangesAsync(setup.ConversationId);

            Assert.Contains(messages, message => message.Id == sent.MessageId);
            Assert.Contains(
                changes,
                change => change.ResourceId == sent.MessageId &&
                          change.ChangeType == MakanApp.Domain.Messaging.MessagingChangeType.MessageCreated);
            Assert.True(await factory.CountPendingRealtimeOutboxAsync(setup.ConversationId) > 0);
        }
        finally
        {
            failureSwitch.Reset();
        }
    }

    private HubConnection CreateHubConnection(string accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(
                new Uri(factory.Server.BaseAddress, "/hubs/messaging"),
                options =>
                {
                    options.Transports = HttpTransportType.LongPolling;
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                })
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
}
