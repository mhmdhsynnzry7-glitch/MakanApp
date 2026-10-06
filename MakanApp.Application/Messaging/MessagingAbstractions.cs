using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;

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

public sealed record MessageReplySummaryStoreRecord(
    Message Message);

public sealed record MessageAttachmentStoreRecord(
    MessageAttachment Attachment,
    FileAsset FileAsset);

public sealed record MessageReactionSummaryStoreRecord(
    MessageReactionType Reaction,
    int Count,
    bool ReactedByCurrentUser);

public sealed record ConversationMessageStoreRecord(
    Message Message,
    SafeMessagingIdentityRecord Sender,
    MessageReplySummaryStoreRecord? Reply,
    IReadOnlyCollection<MessageReactionSummaryStoreRecord> Reactions,
    IReadOnlyCollection<SafeMessagingIdentityRecord> Mentions,
    IReadOnlyCollection<MessageAttachmentStoreRecord> Attachments,
    bool IsPinned);

public sealed record ConversationMessagePageStoreResult(
    IReadOnlyCollection<ConversationMessageStoreRecord> Messages,
    long? NextBeforeSequence);

public sealed record SendMessageStoreCommand(
    Guid ClientMessageId,
    MessageKind Kind,
    string? Text,
    IReadOnlyCollection<Guid> AttachmentIds,
    Guid? ReplyToMessageId,
    IReadOnlyCollection<Guid> MentionedUserIds,
    Guid? ForwardedFromMessageId = null,
    bool AllowSourceAttachments = false);

public sealed record ConversationMediaStoreRecord(
    Message Message,
    MessageAttachment Attachment,
    FileAsset FileAsset);

public sealed record ConversationMediaPageStoreResult(
    IReadOnlyCollection<ConversationMediaStoreRecord> Items,
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

    Task<Message> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        SendMessageStoreCommand command,
        int maximumTextLength,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<Message> EditMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        string? text,
        byte[] expectedVersion,
        int maximumTextLength,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<Message> DeleteMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        byte[] expectedVersion,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<Message> ForwardMessageAsync(
        Guid userId,
        Guid sourceConversationId,
        Guid sourceMessageId,
        AccessContext accessContext,
        Guid destinationConversationId,
        Guid clientMessageId,
        int maximumTextLength,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<MessageReaction> AddReactionAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        MessageReactionType reaction,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task RemoveReactionAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        MessageReactionType reaction,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationPin> PinMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationPin?> UnpinMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<ConversationMediaPageStoreResult> GetConversationMediaAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        MessageKind? kind,
        long? beforeSequence,
        int limit,
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

    Task<MessageReceiptResult> SendMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        SendMessageCommand command,
        CancellationToken cancellationToken);

    Task<MessageReceiptResult> SendTextMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        SendTextMessageCommand command,
        CancellationToken cancellationToken);

    Task<MessageMutationResult> EditMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        EditMessageCommand command,
        CancellationToken cancellationToken);

    Task<MessageMutationResult> DeleteMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        string expectedVersion,
        CancellationToken cancellationToken);

    Task<MessageReceiptResult> ForwardMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid sourceConversationId,
        Guid sourceMessageId,
        ForwardMessageCommand command,
        CancellationToken cancellationToken);

    Task<MessageReactionResult> AddReactionAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        AddReactionCommand command,
        CancellationToken cancellationToken);

    Task RemoveReactionAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        MessageReactionType reaction,
        CancellationToken cancellationToken);

    Task<ConversationPinResult> PinMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken);

    Task<ConversationPinResult> UnpinMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken);

    Task<ConversationMediaPageResult> GetConversationMediaAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        GetConversationMediaQuery query,
        CancellationToken cancellationToken);
}
