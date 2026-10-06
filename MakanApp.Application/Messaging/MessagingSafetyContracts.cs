using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record SearchMessagesQuery(
    string? Query,
    Guid? ConversationId,
    MessageKind? Kind,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Cursor,
    int? Limit);

public sealed record MessageSearchItemResult(
    Guid MessageId,
    Guid ConversationId,
    string? ConversationDisplayName,
    SafeMessagingIdentityResult Sender,
    string? Snippet,
    MessageKind Kind,
    DateTime SentAtUtc,
    long Sequence,
    IReadOnlyCollection<MessageAttachmentResult> Attachments);

public sealed record MessageSearchPageResult(
    IReadOnlyCollection<MessageSearchItemResult> Items,
    int TotalCount,
    string? NextCursor);

public sealed record UserBlockResult(
    Guid BlockId,
    SafeMessagingIdentityResult BlockedUser,
    UserBlockStatus Status,
    DateTime CreatedAtUtc,
    bool AlreadyExisted,
    string Version);

public sealed record BlockedUserResult(
    Guid BlockId,
    SafeMessagingIdentityResult User,
    DateTime CreatedAtUtc,
    string Version);

public sealed record ReportMessageCommand(
    Guid ClientReportId,
    AbuseReportReason Reason,
    string? Description);

public sealed record AbuseReportReceiptResult(
    Guid ReportId,
    Guid ConversationId,
    Guid MessageId,
    AbuseReportReason Reason,
    AbuseReportStatus Status,
    DateTime CreatedAtUtc);
