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
    public async Task<ManagedConversationStoreResult> GetDetailsAsync(
        Guid actorUserId,
        Guid conversationId,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null ||
            conversation.Type is not ConversationType.Group and not ConversationType.Channel)
        {
            throw ConversationNotFound();
        }

        var actor = await LoadActiveParticipantAsync(conversationId, actorUserId, cancellationToken);
        if (actor is null ||
            !await CanAccessScopeAsync(conversation, actorUserId, accessContext, cancellationToken))
        {
            throw ConversationNotFound();
        }

        return await LoadManagedResultAsync(conversation, false, cancellationToken);
    }

    public async Task<ManagedConversationStoreResult> AddMemberAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginManagementTransactionAsync(cancellationToken);
        var (conversation, actor) = await LoadManagedForMutationAsync(
            actorUserId,
            conversationId,
            accessContext,
            cancellationToken);
        if (!managementPolicy.CanAddMember(actor.Role))
        {
            throw ManagementNotAllowed();
        }

        if (await LoadActiveParticipantAsync(conversationId, targetUserId, cancellationToken) is not null)
        {
            throw new MessagingException(
                MessagingErrorCodes.ParticipantAlreadyActive,
                "کاربر از قبل عضو فعال گفتگو است.");
        }

        await EnsureEligibleTargetAsync(
            actorUserId,
            targetUserId,
            conversation.Scope,
            conversation.OrganizationId,
            accessContext,
            nowUtc,
            cancellationToken);
        dbContext.ConversationParticipants.Add(
            ConversationParticipant.CreateActive(
                conversation.Id,
                targetUserId,
                nowUtc,
                ConversationParticipantRole.Member));
        await SaveManagementAsync(cancellationToken);
        var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<ManagedConversationStoreResult> RemoveMemberAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginManagementTransactionAsync(cancellationToken);
        var (conversation, actor) = await LoadManagedForMutationAsync(
            actorUserId,
            conversationId,
            accessContext,
            cancellationToken);
        var target = await LoadActiveParticipantAsync(conversationId, targetUserId, cancellationToken)
            ?? throw ParticipantNotFound();
        if (!managementPolicy.CanRemoveMember(actor.Role, target.Role))
        {
            throw ManagementNotAllowed();
        }

        target.Remove(actorUserId, nowUtc);
        await SaveManagementAsync(cancellationToken);
        var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<ManagedConversationStoreResult> LeaveAsync(
        Guid actorUserId,
        Guid conversationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginManagementTransactionAsync(cancellationToken);
        var (conversation, actor) = await LoadManagedForMutationAsync(
            actorUserId,
            conversationId,
            accessContext,
            cancellationToken);
        if (!managementPolicy.CanLeave(actor.Role))
        {
            throw new MessagingException(
                MessagingErrorCodes.LastOwnerRequired,
                "مالک باید پیش از خروج، مالکیت را به عضو دیگری منتقل کند.");
        }

        actor.Leave(nowUtc);
        await SaveManagementAsync(cancellationToken);
        var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<ManagedConversationStoreResult> ChangeMemberRoleAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid targetUserId,
        ConversationParticipantRole role,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        _ = nowUtc;
        await using var transaction = await BeginManagementTransactionAsync(cancellationToken);
        var (conversation, actor) = await LoadManagedForMutationAsync(
            actorUserId,
            conversationId,
            accessContext,
            cancellationToken);
        var target = await LoadActiveParticipantAsync(conversationId, targetUserId, cancellationToken)
            ?? throw ParticipantNotFound();
        if (!managementPolicy.CanChangeRole(actor.Role, target.Role, role))
        {
            throw ManagementNotAllowed();
        }

        target.ChangeRole(role);
        await SaveManagementAsync(cancellationToken);
        var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

}
