using MakanApp.Api.Authentication;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MakanApp.Api.Realtime;

[Authorize]
public sealed class MessagingHub(IMessagingService messagingService) : Hub<IMessagingHubClient>
{
    public override async Task OnConnectedAsync()
    {
        var sessionId = AuthenticatedSession.GetSessionId(Context.User!);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            MessagingRealtimeGroups.Session(sessionId),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public async Task SubscribeConversation(Guid conversationId)
    {
        try
        {
            await messagingService.AuthorizeRealtimeSubscriptionAsync(
                AuthenticatedSession.GetUserId(Context.User!),
                AuthenticatedSession.GetSessionId(Context.User!),
                conversationId,
                Context.ConnectionAborted);
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                MessagingRealtimeGroups.Conversation(conversationId),
                Context.ConnectionAborted);
        }
        catch (IdentityException)
        {
            throw new HubException(IdentityErrorCodes.AuthRequired);
        }
        catch (MessagingException)
        {
            throw new HubException(MessagingErrorCodes.RealtimeSubscriptionNotAllowed);
        }
    }

    public Task UnsubscribeConversation(Guid conversationId) =>
        Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            MessagingRealtimeGroups.Conversation(conversationId),
            Context.ConnectionAborted);
}
