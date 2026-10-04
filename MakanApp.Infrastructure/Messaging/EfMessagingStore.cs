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
    ICommunicationEligibilityPolicy eligibilityPolicy) : IMessagingStore
{
    public async Task<DirectConversationStoreResult> StartOrGetDirectConversationAsync(
        Guid actorUserId,
        Guid targetUserId,
        ConversationScope scope,
        AccessContext accessContext,
        DateTime nowUtc,
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
            await transaction.CommitAsync(cancellationToken);
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
    }

    public async Task<Message> SendTextMessageAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        Guid clientMessageId,
        string text,
        int maximumTextLength,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var conversation = await dbContext.Conversations
            .FromSqlInterpolated(
                $"SELECT * FROM [messaging].[Conversations] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {conversationId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (conversation is null)
        {
            throw ConversationNotFound();
        }

        var isActiveParticipant = await dbContext.ConversationParticipants.AnyAsync(
            participant => participant.ConversationId == conversationId &&
                           participant.UserId == userId &&
                           participant.Status == ConversationParticipantStatus.Active &&
                           participant.LeftAtUtc == null,
            cancellationToken);
        if (!isActiveParticipant || conversation.Type != ConversationType.Direct)
        {
            throw ConversationNotFound();
        }

        if (conversation.Scope == ConversationScope.Organization &&
            (accessContext.WorkspaceType != WorkspaceType.Organization ||
             accessContext.OrganizationId != conversation.OrganizationId))
        {
            throw new MessagingException(
                MessagingErrorCodes.OrganizationScopeMismatch,
                "برای ارسال پیام باید فضای سازمانی همان گفتگو فعال باشد.");
        }

        var existing = await dbContext.Messages
            .FromSqlInterpolated(
                $"SELECT * FROM [messaging].[Messages] WITH (UPDLOCK, HOLDLOCK) WHERE [ConversationId] = {conversationId} AND [SenderUserId] = {userId} AND [ClientMessageId] = {clientMessageId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (existing.Kind != MessageKind.Text ||
                !string.Equals(existing.Text, text, StringComparison.Ordinal))
            {
                throw new MessagingException(
                    MessagingErrorCodes.MessageIdempotencyConflict,
                    "این شناسه پیام قبلاً برای محتوای دیگری استفاده شده است.");
            }

            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        if (conversation.Status != ConversationStatus.Active)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageNotAllowed,
                "این گفتگو در حال حاضر پیام تازه نمی‌پذیرد.");
        }

        var targetUserId = conversation.GetDirectPair().Other(userId);
        var facts = await LoadEligibilityFactsAsync(
            userId,
            targetUserId,
            conversation.Scope,
            conversation.OrganizationId,
            accessContext,
            nowUtc,
            cancellationToken);
        if (!eligibilityPolicy.CanStartOrSend(facts, MessagingEligibilityOperation.Send))
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageNotAllowed,
                "رابطه ارتباطی معتبر برای ارسال پیام وجود ندارد.");
        }

        var message = Message.CreateText(
            conversationId,
            userId,
            clientMessageId,
            conversation.AllocateNextMessageSequence(),
            text,
            maximumTextLength,
            nowUtc);
        dbContext.Messages.Add(message);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return message;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new MessagingException(
                MessagingErrorCodes.ConcurrencyConflict,
                "گفتگو هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.",
                exception);
        }
        catch (DbUpdateException exception) when (IsUniqueConflict(exception))
        {
            throw new MessagingException(
                MessagingErrorCodes.ConcurrencyConflict,
                "ثبت هم‌زمان پیام با تعارض روبه‌رو شد؛ دوباره تلاش کنید.",
                exception);
        }
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
                                  participant.LeftAtUtc == null)
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

    private static MessagingException RecipientNotAvailable() =>
        new(
            MessagingErrorCodes.DirectRecipientNotAvailable,
            "مخاطب موردنظر برای شروع گفتگو در دسترس نیست.");

    private static MessagingException ConversationNotFound() =>
        new(MessagingErrorCodes.ConversationNotFound, "گفتگو یافت نشد.");
}
