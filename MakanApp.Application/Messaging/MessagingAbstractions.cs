using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record SafeMessagingIdentityRecord(
    Guid UserId,
    string Username,
    string DisplayName);

public sealed record DirectConversationStoreResult(
    Conversation Conversation,
    SafeMessagingIdentityRecord OtherParticipant,
    bool AlreadyExisted);

public sealed record ConversationSummaryStoreRecord(
    Conversation Conversation,
    SafeMessagingIdentityRecord? OtherParticipant,
    string? LastMessagePreview,
    DateTime? LastMessageAtUtc,
    long? LastMessageSequence);

public sealed record ConversationMessagePageStoreResult(
    IReadOnlyCollection<Message> Messages,
    long? NextBeforeSequence);

public interface IMessagingStore
{
    Task<DirectConversationStoreResult> StartOrGetDirectConversationAsync(
        Guid actorUserId,
        Guid targetUserId,
        ConversationScope scope,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ConversationSummaryStoreRecord>> GetMyConversationsAsync(
        Guid userId,
        AccessContext accessContext,
        CancellationToken cancellationToken);

    Task<ConversationMessagePageStoreResult> GetConversationMessagesAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        long? beforeSequence,
        int limit,
        CancellationToken cancellationToken);

    Task<Message> SendTextMessageAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        Guid clientMessageId,
        string text,
        int maximumTextLength,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}

public interface IMessagingService
{
    Task<DirectConversationResult> StartOrGetDirectConversationAsync(
        Guid userId,
        Guid sessionId,
        StartDirectConversationCommand command,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ConversationSummaryResult>> GetMyConversationsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<ConversationMessagePageResult> GetConversationMessagesAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        GetConversationMessagesQuery query,
        CancellationToken cancellationToken);

    Task<MessageReceiptResult> SendTextMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        SendTextMessageCommand command,
        CancellationToken cancellationToken);
}
