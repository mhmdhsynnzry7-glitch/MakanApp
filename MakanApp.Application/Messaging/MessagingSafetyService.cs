using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed class MessagingSafetyService(
    IAccessContextResolver accessContextResolver,
    IMessagingSafetyStore store,
    MessagingOptions options,
    TimeProvider timeProvider) : IMessagingSafetyService
{
    private const int SearchSnippetLength = 240;

    public async Task<MessageSearchPageResult> SearchMessagesAsync(
        Guid userId,
        Guid sessionId,
        SearchMessagesQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = MessageSearchText.Normalize(query.Query);
        if (normalizedQuery is null)
        {
            throw Error(MessagingErrorCodes.SearchQueryRequired, "عبارت جستجو الزامی است.");
        }

        if (normalizedQuery.Length > options.MaximumSearchQueryLength ||
            query.ConversationId == Guid.Empty ||
            query.Kind.HasValue && !Enum.IsDefined(query.Kind.Value) ||
            !IsUtc(query.FromUtc) || !IsUtc(query.ToUtc) ||
            query.FromUtc.HasValue && query.ToUtc.HasValue && query.FromUtc >= query.ToUtc)
        {
            throw Error(MessagingErrorCodes.SearchQueryInvalid, "پارامترهای جستجو معتبر نیستند.");
        }

        MessageSearchPosition? position = null;
        if (query.Cursor is not null && !MessageSearchCursor.TryDecode(query.Cursor, out position))
        {
            throw Error(MessagingErrorCodes.SearchCursorInvalid, "نشانگر جستجو معتبر نیست.");
        }

        var limit = query.Limit ?? options.DefaultSearchLimit;
        if (limit <= 0 || limit > options.MaximumSearchLimit)
        {
            throw Error(MessagingErrorCodes.SearchQueryInvalid, "تعداد نتایج جستجو معتبر نیست.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var page = await store.SearchMessagesAsync(
            userId,
            context,
            new MessageSearchStoreQuery(
                normalizedQuery,
                query.ConversationId,
                query.Kind,
                query.FromUtc,
                query.ToUtc,
                position,
                limit),
            cancellationToken);
        return new MessageSearchPageResult(
            page.Items.Select(MapSearchItem).ToArray(),
            page.TotalCount,
            page.NextPosition is null ? null : MessageSearchCursor.Encode(page.NextPosition));
    }

    public async Task<UserBlockResult> BlockUserAsync(
        Guid userId,
        Guid sessionId,
        Guid blockedUserId,
        CancellationToken cancellationToken)
    {
        if (blockedUserId == Guid.Empty || blockedUserId == userId)
        {
            throw Error(MessagingErrorCodes.UserBlockNotAllowed, "این کاربر قابل مسدودسازی نیست.");
        }

        _ = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var result = await store.BlockUserAsync(
            userId,
            blockedUserId,
            UtcNow(),
            cancellationToken);
        return new UserBlockResult(
            result.Block.Id,
            Map(result.BlockedUser),
            result.Block.Status,
            result.Block.CreatedAtUtc,
            result.AlreadyExisted,
            Convert.ToBase64String(result.Block.RowVersion));
    }

    public async Task UnblockUserAsync(
        Guid userId,
        Guid sessionId,
        Guid blockedUserId,
        CancellationToken cancellationToken)
    {
        if (blockedUserId == Guid.Empty || blockedUserId == userId)
        {
            throw Error(MessagingErrorCodes.UserBlockNotAllowed, "این کاربر قابل رفع مسدودی نیست.");
        }

        _ = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        await store.UnblockUserAsync(userId, blockedUserId, UtcNow(), cancellationToken);
    }

    public async Task<IReadOnlyCollection<BlockedUserResult>> GetBlockedUsersAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        _ = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var records = await store.GetBlockedUsersAsync(userId, cancellationToken);
        return records.Select(record => new BlockedUserResult(
            record.Block.Id,
            Map(record.BlockedUser),
            record.Block.CreatedAtUtc,
            Convert.ToBase64String(record.Block.RowVersion))).ToArray();
    }

    public async Task<AbuseReportReceiptResult> ReportMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        ReportMessageCommand command,
        CancellationToken cancellationToken)
    {
        if (conversationId == Guid.Empty || messageId == Guid.Empty ||
            command.ClientReportId == Guid.Empty || !Enum.IsDefined(command.Reason))
        {
            throw Error(MessagingErrorCodes.ReportNotAllowed, "درخواست گزارش پیام معتبر نیست.");
        }

        var description = string.IsNullOrWhiteSpace(command.Description)
            ? null
            : command.Description.Trim();
        if (description?.Length > AbuseReport.MaximumDescriptionLength)
        {
            throw Error(MessagingErrorCodes.ReportNotAllowed, "توضیح گزارش از طول مجاز بیشتر است.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var report = await store.ReportMessageAsync(
            userId,
            conversationId,
            messageId,
            context,
            new ReportMessageStoreCommand(
                command.ClientReportId,
                command.Reason,
                description,
                HashReportPayload(conversationId, messageId, command.Reason, description)),
            UtcNow(),
            cancellationToken);
        return Map(report);
    }

    public async Task<AbuseReportReceiptResult> GetMyReportAsync(
        Guid userId,
        Guid sessionId,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        if (reportId == Guid.Empty)
        {
            throw Error(MessagingErrorCodes.ReportMessageNotFound, "گزارش یافت نشد.");
        }

        _ = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return Map(await store.GetMyReportAsync(userId, reportId, cancellationToken));
    }

    private static MessageSearchItemResult MapSearchItem(MessageSearchStoreRecord record) => new(
        record.Message.Id,
        record.Conversation.Id,
        record.Conversation.Title,
        Map(record.Sender),
        CreateSnippet(record.Message.Text),
        record.Message.Kind,
        record.Message.SentAtUtc,
        record.Message.Sequence,
        record.Attachments.Select(item => new MessageAttachmentResult(
            item.FileAsset.Id,
            item.Attachment.Kind,
            item.FileAsset.OriginalFileName,
            item.FileAsset.ContentType,
            item.FileAsset.SizeBytes,
            item.Attachment.CreatedAtUtc)).ToArray());

    private static string? CreateSnippet(string? text) =>
        text is null || text.Length <= SearchSnippetLength ? text : text[..SearchSnippetLength];

    private static AbuseReportReceiptResult Map(AbuseReport report) => new(
        report.Id,
        report.ConversationId,
        report.MessageId,
        report.Reason,
        report.Status,
        report.CreatedAtUtc);

    private static SafeMessagingIdentityResult Map(SafeMessagingIdentityRecord identity) =>
        new(identity.UserId, identity.Username, identity.DisplayName);

    private static byte[] HashReportPayload(
        Guid conversationId,
        Guid messageId,
        AbuseReportReason reason,
        string? description)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(conversationId.ToByteArray());
            writer.Write(messageId.ToByteArray());
            writer.Write((int)reason);
            writer.Write(description ?? string.Empty);
        }

        return SHA256.HashData(stream.ToArray());
    }

    private static bool IsUtc(DateTime? value) =>
        !value.HasValue || value.Value.Kind == DateTimeKind.Utc;

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static MessagingException Error(string code, string message) => new(code, message);
}
