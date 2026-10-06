using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record StartDirectConversationCommand(
    Guid TargetUserId,
    ConversationScope Scope);

public sealed record SafeMessagingIdentityResult(
    Guid UserId,
    string Username,
    string DisplayName);

public sealed record DirectConversationResult(
    Guid ConversationId,
    ConversationType Type,
    ConversationScope Scope,
    Guid? OrganizationId,
    SafeMessagingIdentityResult OtherParticipant,
    DateTime CreatedAtUtc,
    bool AlreadyExisted);

public sealed record ConversationSummaryResult(
    Guid ConversationId,
    ConversationType Type,
    ConversationScope Scope,
    Guid? OrganizationId,
    string? Title,
    SafeMessagingIdentityResult? OtherParticipant,
    string? LastMessagePreview,
    DateTime? LastMessageAtUtc,
    long? LastMessageSequence);

public sealed record GetConversationMessagesQuery(
    long? BeforeSequence,
    int? Limit);

public sealed record MessageReplySummaryResult(
    Guid MessageId,
    Guid SenderUserId,
    MessageKind? Kind,
    string? Text,
    bool IsDeleted);

public sealed record MessageReactionSummaryResult(
    MessageReactionType Reaction,
    int Count,
    bool ReactedByCurrentUser);

public sealed record MessageAttachmentResult(
    Guid FileAssetId,
    MessageKind Kind,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc);

public sealed record ConversationMessageResult(
    Guid MessageId,
    Guid ConversationId,
    Guid SenderUserId,
    SafeMessagingIdentityResult Sender,
    Guid ClientMessageId,
    long Sequence,
    MessageKind? Kind,
    string? Text,
    DateTime SentAtUtc,
    DateTime? EditedAtUtc,
    DateTime? DeletedAtUtc,
    bool IsEdited,
    bool IsDeleted,
    MessageReplySummaryResult? Reply,
    bool IsForwarded,
    IReadOnlyCollection<MessageReactionSummaryResult> Reactions,
    IReadOnlyCollection<SafeMessagingIdentityResult> Mentions,
    IReadOnlyCollection<MessageAttachmentResult> Attachments,
    bool IsPinned,
    string Version);

public sealed record ConversationMessagePageResult(
    IReadOnlyCollection<ConversationMessageResult> Messages,
    long? NextBeforeSequence);

public sealed class SendMessageCommand
{
    public Guid ClientMessageId { get; init; }
    public MessageKind Kind { get; init; } = MessageKind.Text;
    public string? Text { get; init; }
    public IReadOnlyCollection<Guid>? AttachmentIds { get; init; }
    public Guid? ReplyToMessageId { get; init; }
    public IReadOnlyCollection<Guid>? MentionedUserIds { get; init; }
}

// این قرارداد برای سازگاری کلاینت‌های STEP 7A باقی می‌ماند؛ مسیر اجرا مشترک است.
public sealed record SendTextMessageCommand(Guid ClientMessageId, string Text);

public sealed record EditMessageCommand(string? Text, string ExpectedVersion);

public sealed record ForwardMessageCommand(
    Guid DestinationConversationId,
    Guid ClientMessageId);

public sealed record AddReactionCommand(MessageReactionType Reaction);

public sealed record MessageReactionResult(
    Guid MessageId,
    MessageReactionType Reaction,
    DateTime CreatedAtUtc);

public sealed record ConversationPinResult(
    Guid ConversationId,
    Guid MessageId,
    Guid PinnedByUserId,
    DateTime PinnedAtUtc,
    bool IsPinned);

public sealed record MessageMutationResult(
    Guid MessageId,
    long Sequence,
    DateTime? EditedAtUtc,
    DateTime? DeletedAtUtc,
    bool IsDeleted,
    string Version);

public sealed record GetConversationMediaQuery(
    MessageKind? Kind,
    long? BeforeSequence,
    int? Limit);

public sealed record ConversationMediaItemResult(
    Guid MessageId,
    long Sequence,
    Guid SenderUserId,
    DateTime SentAtUtc,
    MessageAttachmentResult Attachment);

public sealed record ConversationMediaPageResult(
    IReadOnlyCollection<ConversationMediaItemResult> Items,
    long? NextBeforeSequence);

public enum MessageReceiptStatus
{
    Sent = 1
}

public sealed record MessageReceiptResult(
    Guid MessageId,
    Guid ConversationId,
    Guid ClientMessageId,
    long Sequence,
    MessageKind Kind,
    DateTime SentAtUtc,
    MessageReceiptStatus Status,
    string Version);
