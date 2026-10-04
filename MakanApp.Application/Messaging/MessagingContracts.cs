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
    SafeMessagingIdentityResult OtherParticipant,
    string? LastMessagePreview,
    DateTime? LastMessageAtUtc,
    long? LastMessageSequence);

public sealed record GetConversationMessagesQuery(
    long? BeforeSequence,
    int? Limit);

public sealed record ConversationMessageResult(
    Guid MessageId,
    Guid ConversationId,
    Guid SenderUserId,
    Guid ClientMessageId,
    long Sequence,
    MessageKind Kind,
    string Text,
    DateTime SentAtUtc);

public sealed record ConversationMessagePageResult(
    IReadOnlyCollection<ConversationMessageResult> Messages,
    long? NextBeforeSequence);

public sealed record SendTextMessageCommand(
    Guid ClientMessageId,
    string Text);

public enum MessageReceiptStatus
{
    Sent = 1
}

public sealed record MessageReceiptResult(
    Guid MessageId,
    Guid ConversationId,
    Guid ClientMessageId,
    long Sequence,
    DateTime SentAtUtc,
    MessageReceiptStatus Status);
