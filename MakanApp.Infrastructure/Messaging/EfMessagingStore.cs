using System.Data;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore(
    MakanDbContext dbContext,
    ICommunicationEligibilityPolicy eligibilityPolicy,
    IConversationManagementPolicy managementPolicy,
    MessagingOutboxWakeSignal outboxWakeSignal) : IMessagingStore, IConversationManagementStore
{
    public Task<DirectConversationStoreResult> StartOrGetDirectConversationAsync(
        Guid actorUserId,
        Guid targetUserId,
        ConversationScope scope,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        StartOrGetDirectConversationCoreAsync(
            actorUserId,
            targetUserId,
            scope,
            accessContext,
            nowUtc,
            0,
            cancellationToken);

    private async Task<DirectConversationStoreResult> StartOrGetDirectConversationCoreAsync(
        Guid actorUserId,
        Guid targetUserId,
        ConversationScope scope,
        AccessContext accessContext,
        DateTime nowUtc,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var organizationId = scope == ConversationScope.Organization
            ? accessContext.OrganizationId
            : null;
        var pair = DirectUserPair.Create(actorUserId, targetUserId);

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var facts = await LoadEligibilityFactsAsync(
                actorUserId,
                targetUserId,
                scope,
                organizationId,
                accessContext,
                nowUtc,
                cancellationToken);
            if (!eligibilityPolicy.CanStartOrSend(facts, MessagingEligibilityOperation.Start))
            {
                throw RecipientNotAvailable();
            }

            var identity = await LoadSafeIdentityAsync(targetUserId, cancellationToken);
            var existing = await LockDirectConversationAsync(
                scope,
                organizationId,
                pair,
                cancellationToken);
            if (existing is not null)
            {
                await EnsureDirectParticipantsAsync(existing.Id, pair, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new DirectConversationStoreResult(existing, identity, true);
            }

            var conversation = Conversation.CreateDirect(
                scope,
                organizationId,
                actorUserId,
                targetUserId,
                nowUtc);
            dbContext.Conversations.Add(conversation);
            dbContext.ConversationParticipants.AddRange(
                ConversationParticipant.CreateActive(conversation.Id, pair.LowerUserId, nowUtc),
                ConversationParticipant.CreateActive(conversation.Id, pair.HigherUserId, nowUtc));
            await dbContext.SaveChangesAsync(cancellationToken);
            await AppendChangeAsync(
                conversation,
                MessagingChangeType.ConversationChanged,
                conversation.Id,
                null,
                nowUtc,
                actorUserId,
                null,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            outboxWakeSignal.Signal();
            return new DirectConversationStoreResult(conversation, identity, false);
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            var existing = await FindDirectConversationAsync(
                scope,
                organizationId,
                pair,
                cancellationToken);
            if (existing is null)
            {
                throw new MessagingException(
                    MessagingErrorCodes.DirectConversationConflict,
                    "ساخت هم‌زمان گفتگو با تعارض روبه‌رو شد؛ دوباره تلاش کنید.",
                    exception);
            }

            var identity = await LoadSafeIdentityAsync(targetUserId, cancellationToken);
            return new DirectConversationStoreResult(existing, identity, true);
        }
        catch (DbUpdateException exception) when (IsDeadlock(exception))
        {
            return await RetryDirectCreationAfterDeadlockAsync(
                actorUserId,
                targetUserId,
                scope,
                accessContext,
                nowUtc,
                retryCount,
                exception,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            return await RetryDirectCreationAfterDeadlockAsync(
                actorUserId,
                targetUserId,
                scope,
                accessContext,
                nowUtc,
                retryCount,
                exception,
                cancellationToken);
        }
    }

    private async Task<DirectConversationStoreResult> RetryDirectCreationAfterDeadlockAsync(
        Guid actorUserId,
        Guid targetUserId,
        ConversationScope scope,
        AccessContext accessContext,
        DateTime nowUtc,
        int retryCount,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (retryCount >= 3)
        {
            throw ConcurrencyConflict(exception);
        }

        dbContext.ChangeTracker.Clear();
        await Task.Delay(TimeSpan.FromMilliseconds(20 * (retryCount + 1)), cancellationToken);
        return await StartOrGetDirectConversationCoreAsync(
            actorUserId,
            targetUserId,
            scope,
            accessContext,
            nowUtc,
            retryCount + 1,
            cancellationToken);
    }

    private Task<Conversation?> LockDirectConversationAsync(
        ConversationScope scope,
        Guid? organizationId,
        DirectUserPair pair,
        CancellationToken cancellationToken) =>
        dbContext.Conversations
            .FromSqlInterpolated($"""
                SELECT * FROM [messaging].[Conversations] WITH (UPDLOCK, HOLDLOCK)
                WHERE [Type] = 1
                  AND [Scope] = {scope}
                  AND (({organizationId} IS NULL AND [OrganizationId] IS NULL) OR [OrganizationId] = {organizationId})
                  AND [DirectUserLowId] = {pair.LowerUserId}
                  AND [DirectUserHighId] = {pair.HigherUserId}
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<Conversation?> FindDirectConversationAsync(
        ConversationScope scope,
        Guid? organizationId,
        DirectUserPair pair,
        CancellationToken cancellationToken) =>
        dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                conversation => conversation.Type == ConversationType.Direct &&
                                conversation.Scope == scope &&
                                conversation.OrganizationId == organizationId &&
                                conversation.DirectUserLowId == pair.LowerUserId &&
                                conversation.DirectUserHighId == pair.HigherUserId,
                cancellationToken);

    private async Task EnsureDirectParticipantsAsync(
        Guid conversationId,
        DirectUserPair pair,
        CancellationToken cancellationToken)
    {
        var participants = await dbContext.ConversationParticipants
            .AsNoTracking()
            .Where(participant => participant.ConversationId == conversationId &&
                                  participant.Status == ConversationParticipantStatus.Active &&
                                  participant.EndedAtUtc == null)
            .Select(participant => participant.UserId)
            .ToArrayAsync(cancellationToken);
        if (participants.Length != 2 ||
            !participants.Contains(pair.LowerUserId) ||
            !participants.Contains(pair.HigherUserId))
        {
            throw new MessagingException(
                MessagingErrorCodes.DirectConversationConflict,
                "اعضای گفتگوی مستقیم با هویت canonical آن سازگار نیستند.");
        }
    }

    private static bool IsUniqueConflict(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private static bool IsDeadlock(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 1205 };

    private static MessagingException RecipientNotAvailable() =>
        new(
            MessagingErrorCodes.DirectRecipientNotAvailable,
            "مخاطب موردنظر برای شروع گفتگو در دسترس نیست.");

    private static MessagingException ConversationNotFound() =>
        new(MessagingErrorCodes.ConversationNotFound, "گفتگو یافت نشد.");
}
