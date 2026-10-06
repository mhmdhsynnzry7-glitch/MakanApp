using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.SignalR;

namespace MakanApp.Api.Realtime;

public sealed class SignalRMessagingRealtimeNotifier(
    IHubContext<MessagingHub, IMessagingHubClient> hubContext,
    IMessagingRealtimeAudienceResolver audienceResolver,
    TimeProvider timeProvider) : IMessagingRealtimeNotifier
{
    public async Task NotifyConversationChangedAsync(
        MessagingRealtimeNotification notification,
        Guid? audienceUserId,
        CancellationToken cancellationToken)
    {
        var sessionIds = await audienceResolver.GetActiveSessionIdsAsync(
            notification.ConversationId,
            audienceUserId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (sessionIds.Count == 0)
        {
            return;
        }

        var groups = sessionIds
            .Select(MessagingRealtimeGroups.Session)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        await hubContext.Clients.Groups(groups)
            .ConversationChanged(notification)
            .WaitAsync(cancellationToken);
    }
}
