namespace MakanApp.Api.Realtime;

public static class MessagingRealtimeGroups
{
    public static string Session(Guid sessionId) => $"messaging:session:{sessionId:N}";

    public static string Conversation(Guid conversationId) => $"messaging:conversation:{conversationId:N}";
}
