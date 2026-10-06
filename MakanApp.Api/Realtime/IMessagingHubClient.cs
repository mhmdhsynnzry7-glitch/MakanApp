using MakanApp.Application.Messaging;

namespace MakanApp.Api.Realtime;

public interface IMessagingHubClient
{
    Task ConversationChanged(MessagingRealtimeNotification notification);
}
