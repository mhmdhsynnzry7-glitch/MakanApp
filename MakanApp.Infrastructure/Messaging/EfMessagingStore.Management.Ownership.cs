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
    public async Task<OwnershipTransferStoreResult> StartOwnershipTransferAsync(
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
        if (!managementPolicy.CanTransferOwnership(actor.Role) ||
            target.Role == ConversationParticipantRole.Owner)
        {
            throw ManagementNotAllowed();
        }

        if (await dbContext.ConversationOwnershipTransfers.AnyAsync(
                item => item.ConversationId == conversationId &&
                        item.Status == OwnershipTransferStatus.Pending,
                cancellationToken))
        {
            throw new MessagingException(
                MessagingErrorCodes.OwnershipTransferConflict,
                "یک درخواست انتقال مالکیت در انتظار پاسخ است.");
        }

        var transfer = ConversationOwnershipTransfer.CreatePending(
            conversationId,
            actor.Id,
            actor.UserId,
            target.Id,
            target.UserId,
            nowUtc);
        dbContext.ConversationOwnershipTransfers.Add(transfer);
        await SaveManagementAsync(cancellationToken);
        await AppendChangeAsync(
            conversation,
            MessagingChangeType.ConversationChanged,
            transfer.Id,
            VersionOf(transfer.RowVersion),
            nowUtc,
            actorUserId,
            null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outboxWakeSignal.Signal();
        return new OwnershipTransferStoreResult(transfer, false);
    }

    public Task<OwnershipTransferStoreResult> AcceptOwnershipTransferAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid transferId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        CompleteOwnershipTransferAsync(
            actorUserId,
            conversationId,
            transferId,
            accessContext,
            nowUtc,
            true,
            cancellationToken);

    public Task<OwnershipTransferStoreResult> DeclineOwnershipTransferAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid transferId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        CompleteOwnershipTransferAsync(
            actorUserId,
            conversationId,
            transferId,
            accessContext,
            nowUtc,
            false,
            cancellationToken);

    public async Task<ManagedConversationStoreResult> ArchiveAsync(
        Guid actorUserId,
        Guid conversationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginManagementTransactionAsync(cancellationToken);
        var conversation = await LockConversationAsync(conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        var actor = await LoadActiveParticipantAsync(conversationId, actorUserId, cancellationToken)
            ?? throw ConversationNotFound();
        await EnsureManagedContextAsync(conversation, actorUserId, accessContext, false, cancellationToken);
        if (!managementPolicy.CanArchive(actor.Role))
        {
            throw ManagementNotAllowed();
        }

        var changed = conversation.Status != ConversationStatus.Archived;
        conversation.Archive(nowUtc);
        if (changed)
        {
            await SaveManagementAsync(cancellationToken);
            await AppendChangeAsync(
                conversation,
                MessagingChangeType.ConversationChanged,
                conversation.Id,
                null,
                nowUtc,
                actorUserId,
                null,
                cancellationToken);
        }
        var result = await LoadManagedResultAsync(conversation, false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (changed)
        {
            outboxWakeSignal.Signal();
        }

        return result;
    }

    private async Task<OwnershipTransferStoreResult> CompleteOwnershipTransferAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid transferId,
        AccessContext accessContext,
        DateTime nowUtc,
        bool accept,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginManagementTransactionAsync(cancellationToken);
        var conversation = await LockConversationAsync(conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        await EnsureManagedContextAsync(conversation, actorUserId, accessContext, true, cancellationToken);
        var transfer = await dbContext.ConversationOwnershipTransfers
            .SingleOrDefaultAsync(
                item => item.Id == transferId && item.ConversationId == conversationId,
                cancellationToken)
            ?? throw OwnershipTransferNotFound();
        if (transfer.ToUserId != actorUserId)
        {
            throw OwnershipTransferNotFound();
        }

        if ((accept && transfer.Status == OwnershipTransferStatus.Accepted) ||
            (!accept && transfer.Status == OwnershipTransferStatus.Declined))
        {
            await transaction.CommitAsync(cancellationToken);
            return new OwnershipTransferStoreResult(transfer, true);
        }

        if (!transfer.IsPending)
        {
            throw new MessagingException(
                MessagingErrorCodes.OwnershipTransferConflict,
                "درخواست انتقال مالکیت دیگر در انتظار پاسخ نیست.");
        }

        var from = await LoadActiveParticipantAsync(conversationId, transfer.FromUserId, cancellationToken)
            ?? throw OwnershipTransferConflict();
        var to = await LoadActiveParticipantAsync(conversationId, transfer.ToUserId, cancellationToken)
            ?? throw OwnershipTransferConflict();
        if (from.Id != transfer.FromParticipantId ||
            to.Id != transfer.ToParticipantId ||
            from.Role != ConversationParticipantRole.Owner)
        {
            throw OwnershipTransferConflict();
        }

        if (accept)
        {
            from.ChangeRole(ConversationParticipantRole.Admin);
            await SaveManagementAsync(cancellationToken);
            to.ChangeRole(ConversationParticipantRole.Owner);
            transfer.Accept(nowUtc);
        }
        else
        {
            transfer.Decline(nowUtc);
        }

        await SaveManagementAsync(cancellationToken);
        await AppendChangeAsync(
            conversation,
            accept ? MessagingChangeType.ParticipantChanged : MessagingChangeType.ConversationChanged,
            accept ? to.Id : transfer.Id,
            VersionOf(accept ? to.RowVersion : transfer.RowVersion),
            nowUtc,
            actorUserId,
            null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        outboxWakeSignal.Signal();
        return new OwnershipTransferStoreResult(transfer, false);
    }

}
