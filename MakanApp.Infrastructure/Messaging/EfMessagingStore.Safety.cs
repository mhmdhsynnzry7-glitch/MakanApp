using System.Data;
using System.Security.Cryptography;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<MessageSearchStoreResult> SearchMessagesAsync(
        Guid userId,
        AccessContext accessContext,
        MessageSearchStoreQuery query,
        CancellationToken cancellationToken)
    {
        var workspaceType = accessContext.WorkspaceType;
        var organizationId = accessContext.OrganizationId;
        var membershipId = accessContext.MembershipId;
        var normalizedQuery = query.NormalizedQuery;

        var authorizedQuery =
            from message in dbContext.Messages.AsNoTracking()
            join conversation in dbContext.Conversations.AsNoTracking()
                on message.ConversationId equals conversation.Id
            join participant in dbContext.ConversationParticipants.AsNoTracking()
                on new { ConversationId = conversation.Id, UserId = userId }
                equals new { participant.ConversationId, participant.UserId }
            where participant.Status == ConversationParticipantStatus.Active &&
                  participant.EndedAtUtc == null &&
                  message.DeletedAtUtc == null &&
                  (workspaceType == WorkspaceType.Personal &&
                   conversation.Scope == ConversationScope.Personal &&
                   organizationId == null ||
                   workspaceType == WorkspaceType.Organization &&
                   organizationId != null && membershipId != null &&
                   conversation.Scope == ConversationScope.Organization &&
                   conversation.OrganizationId == organizationId &&
                   dbContext.Memberships.Any(membership =>
                       membership.Id == membershipId &&
                       membership.UserId == userId &&
                       membership.OrganizationId == organizationId &&
                       membership.Status == MembershipStatus.Active &&
                       membership.EndedAtUtc == null)) &&
                  (!query.ConversationId.HasValue ||
                   conversation.Id == query.ConversationId.Value) &&
                  (!query.Kind.HasValue || message.Kind == query.Kind.Value) &&
                  (!query.FromUtc.HasValue || message.SentAtUtc >= query.FromUtc.Value) &&
                  (!query.ToUtc.HasValue || message.SentAtUtc < query.ToUtc.Value) &&
                  (message.SearchText != null && message.SearchText.Contains(normalizedQuery) ||
                   (from attachment in dbContext.MessageAttachments
                    join file in dbContext.FileAssets on attachment.FileAssetId equals file.Id
                    where attachment.MessageId == message.Id &&
                          file.Status == FileAssetStatus.Ready &&
                          file.OriginalFileName
                              .Replace("ي", "ی")
                              .Replace("ى", "ی")
                              .Replace("ك", "ک")
                              .Replace("\u200C", " ")
                              .Replace("\r", " ")
                              .Replace("\n", " ")
                              .Replace("\t", " ")
                              .Replace("  ", " ")
                              .Replace("  ", " ")
                              .Contains(normalizedQuery)
                    select attachment.Id).Any())
            select new { Message = message, Conversation = conversation };

        var totalCount = await authorizedQuery.CountAsync(cancellationToken);
        if (query.Position is not null)
        {
            var sentAtUtc = query.Position.SentAtUtc;
            var messageId = query.Position.MessageId;
            authorizedQuery = authorizedQuery.Where(row =>
                row.Message.SentAtUtc < sentAtUtc ||
                row.Message.SentAtUtc == sentAtUtc && row.Message.Id.CompareTo(messageId) < 0);
        }

        var rows = await authorizedQuery
            .OrderByDescending(row => row.Message.SentAtUtc)
            .ThenByDescending(row => row.Message.Id)
            .Take(query.Limit + 1)
            .ToArrayAsync(cancellationToken);
        var pageRows = rows.Take(query.Limit).ToArray();
        if (pageRows.Length == 0)
        {
            return new MessageSearchStoreResult([], totalCount, null);
        }

        var messageIds = pageRows.Select(row => row.Message.Id).ToArray();
        var attachmentRows = await (
            from attachment in dbContext.MessageAttachments.AsNoTracking()
            join file in dbContext.FileAssets.AsNoTracking()
                on attachment.FileAssetId equals file.Id
            where messageIds.Contains(attachment.MessageId) && file.Status == FileAssetStatus.Ready
            select new { attachment, file })
            .ToArrayAsync(cancellationToken);
        var attachments = attachmentRows
            .GroupBy(row => row.attachment.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<MessageAttachmentStoreRecord>)group
                    .OrderBy(row => row.attachment.Id)
                    .Select(row => new MessageAttachmentStoreRecord(row.attachment, row.file))
                    .ToArray());

        var identities = new Dictionary<Guid, SafeMessagingIdentityRecord>();
        foreach (var senderUserId in pageRows.Select(row => row.Message.SenderUserId).Distinct())
        {
            identities[senderUserId] = await LoadSafeIdentityAsync(senderUserId, cancellationToken);
        }

        var results = pageRows.Select(row => new MessageSearchStoreRecord(
            row.Message,
            row.Conversation,
            identities[row.Message.SenderUserId],
            attachments.GetValueOrDefault(row.Message.Id, []))).ToArray();
        var last = pageRows[^1].Message;
        return new MessageSearchStoreResult(
            results,
            totalCount,
            rows.Length > query.Limit
                ? new MessageSearchPosition(last.SentAtUtc, last.Id)
                : null);
    }

    public async Task<UserBlockStoreResult> BlockUserAsync(
        Guid blockerUserId,
        Guid blockedUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var target = await LoadUserSnapshotAsync(blockedUserId, cancellationToken);
        if (target is null)
        {
            throw BlockNotAllowed();
        }

        var existing = await LockActiveBlockAsync(
            blockerUserId,
            blockedUserId,
            cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new UserBlockStoreResult(
                existing,
                new SafeMessagingIdentityRecord(target.UserId, target.Username, target.DisplayName),
                true);
        }

        var block = UserBlock.Create(blockerUserId, blockedUserId, nowUtc);
        dbContext.UserBlocks.Add(block);
        await SaveSafetyAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new UserBlockStoreResult(
            block,
            new SafeMessagingIdentityRecord(target.UserId, target.Username, target.DisplayName),
            false);
    }

    public async Task UnblockUserAsync(
        Guid blockerUserId,
        Guid blockedUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var block = await LockActiveBlockAsync(blockerUserId, blockedUserId, cancellationToken);
        if (block is not null)
        {
            block.End(nowUtc);
            await SaveSafetyAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<BlockedUserStoreRecord>> GetBlockedUsersAsync(
        Guid blockerUserId,
        CancellationToken cancellationToken)
    {
        var blocks = await dbContext.UserBlocks
            .AsNoTracking()
            .Where(block => block.BlockerUserId == blockerUserId &&
                            block.Status == UserBlockStatus.Active &&
                            block.EndedAtUtc == null)
            .OrderByDescending(block => block.CreatedAtUtc)
            .Take(500)
            .ToArrayAsync(cancellationToken);
        var results = new List<BlockedUserStoreRecord>(blocks.Length);
        foreach (var block in blocks)
        {
            results.Add(new BlockedUserStoreRecord(
                block,
                await LoadSafeIdentityAsync(block.BlockedUserId, cancellationToken)));
        }

        return results;
    }

    public async Task<AbuseReport> ReportMessageAsync(
        Guid reporterUserId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        ReportMessageStoreCommand command,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var existing = await dbContext.AbuseReports
            .FromSqlInterpolated($"SELECT * FROM [messaging].[AbuseReports] WITH (UPDLOCK, HOLDLOCK) WHERE [ReporterUserId] = {reporterUserId} AND [ClientReportId] = {command.ClientReportId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    existing.RequestPayloadHash,
                    command.RequestPayloadHash))
            {
                throw new MessagingException(
                    MessagingErrorCodes.ReportIdempotencyConflict,
                    "این شناسه گزارش قبلاً برای درخواست دیگری استفاده شده است.");
            }

            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        var conversation = await LockConversationAsync(conversationId, cancellationToken)
            ?? throw ReportMessageNotFound();
        var participant = await LoadActiveParticipantAsync(
            conversationId,
            reporterUserId,
            cancellationToken) ?? throw ReportMessageNotFound();
        try
        {
            await EnsureCanReadAdvancedAsync(
                conversation,
                participant,
                reporterUserId,
                accessContext,
                cancellationToken);
        }
        catch (MessagingException)
        {
            throw ReportMessageNotFound();
        }

        var message = await LockMessageAsync(conversationId, messageId, cancellationToken);
        if (message is null || message.IsDeleted)
        {
            throw ReportMessageNotFound();
        }

        var report = AbuseReport.Create(
            reporterUserId,
            message.SenderUserId,
            conversationId,
            messageId,
            command.ClientReportId,
            command.Reason,
            command.Description,
            message.Kind,
            message.Text,
            message.RowVersion,
            command.RequestPayloadHash,
            nowUtc);
        dbContext.AbuseReports.Add(report);
        await SaveSafetyAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return report;
    }

    public async Task<AbuseReport> GetMyReportAsync(
        Guid reporterUserId,
        Guid reportId,
        CancellationToken cancellationToken) =>
        await dbContext.AbuseReports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                report => report.Id == reportId && report.ReporterUserId == reporterUserId,
                cancellationToken) ?? throw ReportMessageNotFound();

    private Task<UserBlock?> LockActiveBlockAsync(
        Guid blockerUserId,
        Guid blockedUserId,
        CancellationToken cancellationToken) =>
        dbContext.UserBlocks
            .FromSqlInterpolated($"SELECT * FROM [messaging].[UserBlocks] WITH (UPDLOCK, HOLDLOCK) WHERE [BlockerUserId] = {blockerUserId} AND [BlockedUserId] = {blockedUserId} AND [EndedAtUtc] IS NULL")
            .SingleOrDefaultAsync(cancellationToken);

    private Task<bool> HasEffectiveUserBlockAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken) =>
        dbContext.UserBlocks.AnyAsync(
            block => block.Status == UserBlockStatus.Active &&
                     block.EndedAtUtc == null &&
                     (block.BlockerUserId == firstUserId && block.BlockedUserId == secondUserId ||
                      block.BlockerUserId == secondUserId && block.BlockedUserId == firstUserId),
            cancellationToken);

    private async Task SaveSafetyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw ConcurrencyConflict(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            throw ConcurrencyConflict(exception);
        }
    }

    private static MessagingException BlockNotAllowed() => new(
        MessagingErrorCodes.UserBlockNotAllowed,
        "این کاربر قابل مسدودسازی نیست.");

    private static MessagingException ReportMessageNotFound() => new(
        MessagingErrorCodes.ReportMessageNotFound,
        "پیام قابل گزارش یافت نشد.");

}
