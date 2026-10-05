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
    private async Task<(Conversation Conversation, ConversationParticipant Actor)> LoadManagedForMutationAsync(
        Guid actorUserId,
        Guid conversationId,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var conversation = await LockConversationAsync(conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        var actor = await LoadActiveParticipantAsync(conversationId, actorUserId, cancellationToken)
            ?? throw ConversationNotFound();
        await EnsureManagedContextAsync(conversation, actorUserId, accessContext, true, cancellationToken);
        return (conversation, actor);
    }

    private async Task EnsureManagedContextAsync(
        Conversation conversation,
        Guid actorUserId,
        AccessContext accessContext,
        bool requireActive,
        CancellationToken cancellationToken)
    {
        if (conversation.Type is not ConversationType.Group and not ConversationType.Channel)
        {
            throw ConversationNotFound();
        }

        if (conversation.ManagementPolicy != ConversationManagementPolicy.UserManaged)
        {
            throw ManagementNotAllowed();
        }

        if (requireActive && conversation.Status != ConversationStatus.Active)
        {
            throw new MessagingException(
                MessagingErrorCodes.ConversationArchived,
                "گفتگوی بایگانی‌شده قابل تغییر نیست.");
        }

        if (!await CanAccessScopeAsync(conversation, actorUserId, accessContext, cancellationToken))
        {
            throw ConversationNotFound();
        }
    }

    private async Task<bool> CanAccessScopeAsync(
        Conversation conversation,
        Guid actorUserId,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        if (conversation.Scope == ConversationScope.Personal)
        {
            return accessContext.WorkspaceType == WorkspaceType.Personal &&
                   !accessContext.OrganizationId.HasValue;
        }

        return conversation.OrganizationId.HasValue &&
               accessContext.WorkspaceType == WorkspaceType.Organization &&
               accessContext.OrganizationId == conversation.OrganizationId &&
               accessContext.MembershipId.HasValue &&
               await dbContext.Memberships.AnyAsync(
                   membership => membership.Id == accessContext.MembershipId.Value &&
                                 membership.UserId == actorUserId &&
                                 membership.OrganizationId == conversation.OrganizationId.Value &&
                                 membership.Status == MembershipStatus.Active &&
                                 membership.EndedAtUtc == null,
                   cancellationToken);
    }

    private async Task<bool> HasActiveActorContextAsync(
        Guid actorUserId,
        ConversationScope scope,
        Guid? organizationId,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        if (scope == ConversationScope.Personal)
        {
            return accessContext.WorkspaceType == WorkspaceType.Personal;
        }

        return organizationId.HasValue &&
               accessContext.OrganizationId == organizationId &&
               accessContext.MembershipId.HasValue &&
               accessContext.ActiveRole.HasValue &&
               await dbContext.Memberships.AnyAsync(
                   membership => membership.Id == accessContext.MembershipId.Value &&
                                 membership.UserId == actorUserId &&
                                 membership.OrganizationId == organizationId.Value &&
                                 membership.Status == MembershipStatus.Active &&
                                 membership.EndedAtUtc == null,
                   cancellationToken) &&
               await dbContext.RoleAssignments.AnyAsync(
                   assignment => assignment.MembershipId == accessContext.MembershipId.Value &&
                                 assignment.Role == accessContext.ActiveRole.Value &&
                                 assignment.Status == RoleAssignmentStatus.Active &&
                                 assignment.EndedAtUtc == null,
                   cancellationToken);
    }

    private async Task EnsureEligibleTargetAsync(
        Guid actorUserId,
        Guid targetUserId,
        ConversationScope scope,
        Guid? organizationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
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
            throw new MessagingException(
                MessagingErrorCodes.DirectRecipientNotAvailable,
                "کاربر موردنظر برای عضویت در این گفتگو در دسترس نیست.");
        }
    }

    private Task<Conversation?> LockCreationAsync(
        Guid actorUserId,
        Guid clientOperationId,
        CancellationToken cancellationToken) =>
        dbContext.Conversations
            .FromSqlInterpolated($"""
                SELECT * FROM [messaging].[Conversations] WITH (UPDLOCK, HOLDLOCK)
                WHERE [CreatedByUserId] = {actorUserId}
                  AND [ClientOperationId] = {clientOperationId}
                  AND [ManagementPolicy] = 1
                """)
            .SingleOrDefaultAsync(cancellationToken);

    private Task<Conversation?> LockConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken) =>
        dbContext.Conversations
            .FromSqlInterpolated(
                $"SELECT * FROM [messaging].[Conversations] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {conversationId}")
            .SingleOrDefaultAsync(cancellationToken);

    private Task<ConversationParticipant?> LoadActiveParticipantAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.ConversationParticipants.SingleOrDefaultAsync(
            participant => participant.ConversationId == conversationId &&
                           participant.UserId == userId &&
                           participant.Status == ConversationParticipantStatus.Active &&
                           participant.EndedAtUtc == null,
            cancellationToken);

    private async Task<ManagedConversationStoreResult> LoadManagedResultAsync(
        Conversation conversation,
        bool alreadyExisted,
        CancellationToken cancellationToken)
    {
        var participants = await dbContext.ConversationParticipants
            .Where(participant => participant.ConversationId == conversation.Id &&
                                  participant.Status == ConversationParticipantStatus.Active &&
                                  participant.EndedAtUtc == null)
            .OrderBy(participant => participant.Role)
            .ThenBy(participant => participant.JoinedAtUtc)
            .ToArrayAsync(cancellationToken);
        var result = new List<ConversationParticipantStoreRecord>(participants.Length);
        foreach (var participant in participants)
        {
            result.Add(new ConversationParticipantStoreRecord(
                participant,
                await LoadSafeIdentityAsync(participant.UserId, cancellationToken)));
        }

        return new ManagedConversationStoreResult(conversation, result, alreadyExisted);
    }

    private static void EnsureCreationRetryMatches(Conversation existing, byte[] creationPayloadHash)
    {
        if (existing.CreationPayloadHash is null ||
            !CryptographicOperations.FixedTimeEquals(existing.CreationPayloadHash, creationPayloadHash))
        {
            throw new MessagingException(
                MessagingErrorCodes.ConversationCreationConflict,
                "این شناسه عملیات قبلاً برای درخواست ایجاد متفاوتی استفاده شده است.");
        }
    }

    private Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginManagementTransactionAsync(
        CancellationToken cancellationToken) =>
        dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

    private async Task SaveManagementAsync(CancellationToken cancellationToken)
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

    private static MessagingException ManagementNotAllowed() =>
        new(
            MessagingErrorCodes.ConversationManagementNotAllowed,
            "اجازه مدیریت این گفتگو را ندارید.");

    private static MessagingException ParticipantNotFound() =>
        new(MessagingErrorCodes.ParticipantNotFound, "عضو فعال گفتگو یافت نشد.");

    private static MessagingException OwnershipTransferNotFound() =>
        new(MessagingErrorCodes.OwnershipTransferNotFound, "درخواست انتقال مالکیت یافت نشد.");

    private static MessagingException OwnershipTransferConflict() =>
        new(
            MessagingErrorCodes.OwnershipTransferConflict,
            "وضعیت فعلی اعضا با درخواست انتقال مالکیت سازگار نیست.");

    private static MessagingException ConcurrencyConflict(Exception exception) =>
        new(
            MessagingErrorCodes.ConcurrencyConflict,
            "گفتگو هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.",
            exception);
}
