using System.Data;
using System.Security.Cryptography;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public Task<ManagedConversationStoreResult> CreateAsync(
        Guid actorUserId,
        ConversationType type,
        ConversationScope scope,
        string title,
        string? description,
        IReadOnlyCollection<Guid> initialParticipantUserIds,
        Guid clientOperationId,
        byte[] creationPayloadHash,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        CreateCoreAsync(
            actorUserId,
            type,
            scope,
            title,
            description,
            initialParticipantUserIds,
            clientOperationId,
            creationPayloadHash,
            accessContext,
            nowUtc,
            0,
            cancellationToken);

    private async Task<ManagedConversationStoreResult> CreateCoreAsync(
        Guid actorUserId,
        ConversationType type,
        ConversationScope scope,
        string title,
        string? description,
        IReadOnlyCollection<Guid> initialParticipantUserIds,
        Guid clientOperationId,
        byte[] creationPayloadHash,
        AccessContext accessContext,
        DateTime nowUtc,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var organizationId = scope == ConversationScope.Organization
            ? accessContext.OrganizationId
            : null;

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var existing = await LockCreationAsync(actorUserId, clientOperationId, cancellationToken);
            if (existing is not null)
            {
                EnsureCreationRetryMatches(existing, creationPayloadHash);
                var existingResult = await LoadManagedResultAsync(existing, true, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return existingResult;
            }

            var actor = await LoadUserSnapshotAsync(actorUserId, cancellationToken);
            if (actor is null ||
                !managementPolicy.CanCreate(scope, accessContext, actor.AgeCategory) ||
                !await HasActiveActorContextAsync(actorUserId, scope, organizationId, accessContext, cancellationToken))
            {
                throw ManagementNotAllowed();
            }

            foreach (var targetUserId in initialParticipantUserIds)
            {
                await EnsureEligibleTargetAsync(
                    actorUserId,
                    targetUserId,
                    scope,
                    organizationId,
                    accessContext,
                    nowUtc,
                    cancellationToken);
            }

            var conversation = Conversation.CreateUserManaged(
                type,
                scope,
                organizationId,
                title,
                description,
                actorUserId,
                clientOperationId,
                creationPayloadHash,
                nowUtc);
            dbContext.Conversations.Add(conversation);
            dbContext.ConversationParticipants.Add(
                ConversationParticipant.CreateActive(
                    conversation.Id,
                    actorUserId,
                    nowUtc,
                    ConversationParticipantRole.Owner));
            foreach (var targetUserId in initialParticipantUserIds)
            {
                dbContext.ConversationParticipants.Add(
                    ConversationParticipant.CreateActive(
                        conversation.Id,
                        targetUserId,
                        nowUtc,
                        ConversationParticipantRole.Member));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            var existing = await dbContext.Conversations
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.CreatedByUserId == actorUserId &&
                            item.ClientOperationId == clientOperationId &&
                            item.ManagementPolicy == ConversationManagementPolicy.UserManaged,
                    cancellationToken);
            if (existing is null)
            {
                throw ConcurrencyConflict(exception);
            }

            EnsureCreationRetryMatches(existing, creationPayloadHash);
            return await LoadManagedResultAsync(existing, true, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDeadlock(exception))
        {
            return await RetryManagedCreationAfterDeadlockAsync(
                actorUserId,
                type,
                scope,
                title,
                description,
                initialParticipantUserIds,
                clientOperationId,
                creationPayloadHash,
                accessContext,
                nowUtc,
                retryCount,
                exception,
                cancellationToken);
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            return await RetryManagedCreationAfterDeadlockAsync(
                actorUserId,
                type,
                scope,
                title,
                description,
                initialParticipantUserIds,
                clientOperationId,
                creationPayloadHash,
                accessContext,
                nowUtc,
                retryCount,
                exception,
                cancellationToken);
        }
    }

    private async Task<ManagedConversationStoreResult> RetryManagedCreationAfterDeadlockAsync(
        Guid actorUserId,
        ConversationType type,
        ConversationScope scope,
        string title,
        string? description,
        IReadOnlyCollection<Guid> initialParticipantUserIds,
        Guid clientOperationId,
        byte[] creationPayloadHash,
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
        return await CreateCoreAsync(
            actorUserId,
            type,
            scope,
            title,
            description,
            initialParticipantUserIds,
            clientOperationId,
            creationPayloadHash,
            accessContext,
            nowUtc,
            retryCount + 1,
            cancellationToken);
    }

}
