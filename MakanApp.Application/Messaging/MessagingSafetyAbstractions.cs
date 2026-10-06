using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record MessageSearchPosition(DateTime SentAtUtc, Guid MessageId);

public sealed record MessageSearchStoreQuery(
    string NormalizedQuery,
    Guid? ConversationId,
    MessageKind? Kind,
    DateTime? FromUtc,
    DateTime? ToUtc,
    MessageSearchPosition? Position,
    int Limit);

public sealed record MessageSearchStoreRecord(
    Message Message,
    Conversation Conversation,
    SafeMessagingIdentityRecord Sender,
    IReadOnlyCollection<MessageAttachmentStoreRecord> Attachments);

public sealed record MessageSearchStoreResult(
    IReadOnlyCollection<MessageSearchStoreRecord> Items,
    int TotalCount,
    MessageSearchPosition? NextPosition);

public sealed record UserBlockStoreResult(
    UserBlock Block,
    SafeMessagingIdentityRecord BlockedUser,
    bool AlreadyExisted);

public sealed record BlockedUserStoreRecord(
    UserBlock Block,
    SafeMessagingIdentityRecord BlockedUser);

public sealed record ReportMessageStoreCommand(
    Guid ClientReportId,
    AbuseReportReason Reason,
    string? Description,
    byte[] RequestPayloadHash);

public interface IMessagingSafetyStore
{
    Task<MessageSearchStoreResult> SearchMessagesAsync(
        Guid userId,
        AccessContext accessContext,
        MessageSearchStoreQuery query,
        CancellationToken cancellationToken);

    Task<UserBlockStoreResult> BlockUserAsync(
        Guid blockerUserId,
        Guid blockedUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task UnblockUserAsync(
        Guid blockerUserId,
        Guid blockedUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BlockedUserStoreRecord>> GetBlockedUsersAsync(
        Guid blockerUserId,
        CancellationToken cancellationToken);

    Task<AbuseReport> ReportMessageAsync(
        Guid reporterUserId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        ReportMessageStoreCommand command,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<AbuseReport> GetMyReportAsync(
        Guid reporterUserId,
        Guid reportId,
        CancellationToken cancellationToken);
}

public interface IMessagingSafetyService
{
    Task<MessageSearchPageResult> SearchMessagesAsync(
        Guid userId,
        Guid sessionId,
        SearchMessagesQuery query,
        CancellationToken cancellationToken);

    Task<UserBlockResult> BlockUserAsync(
        Guid userId,
        Guid sessionId,
        Guid blockedUserId,
        CancellationToken cancellationToken);

    Task UnblockUserAsync(
        Guid userId,
        Guid sessionId,
        Guid blockedUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<BlockedUserResult>> GetBlockedUsersAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<AbuseReportReceiptResult> ReportMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        ReportMessageCommand command,
        CancellationToken cancellationToken);

    Task<AbuseReportReceiptResult> GetMyReportAsync(
        Guid userId,
        Guid sessionId,
        Guid reportId,
        CancellationToken cancellationToken);
}
