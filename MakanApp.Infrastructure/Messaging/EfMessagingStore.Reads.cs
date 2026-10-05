using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    private const int LastMessagePreviewLength = 120;

    public async Task<IReadOnlyCollection<ConversationSummaryStoreRecord>> GetMyConversationsAsync(
        Guid userId,
        AccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var workspaceType = accessContext.WorkspaceType;
        var organizationId = accessContext.OrganizationId;
        var membershipId = accessContext.MembershipId;
        var conversations = await (
            from participant in dbContext.ConversationParticipants
            join conversation in dbContext.Conversations
                on participant.ConversationId equals conversation.Id
            where participant.UserId == userId &&
                  participant.Status == ConversationParticipantStatus.Active &&
                  participant.EndedAtUtc == null &&
                  conversation.Status == ConversationStatus.Active &&
                  (workspaceType == WorkspaceType.Personal &&
                   conversation.Scope == ConversationScope.Personal ||
                   workspaceType == WorkspaceType.Organization &&
                   organizationId != null &&
                   membershipId != null &&
                   conversation.Scope == ConversationScope.Organization &&
                   conversation.OrganizationId == organizationId &&
                   dbContext.Memberships.Any(membership =>
                       membership.Id == membershipId &&
                       membership.UserId == userId &&
                       membership.OrganizationId == organizationId &&
                       membership.Status == MembershipStatus.Active &&
                       membership.EndedAtUtc == null))
            select conversation)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        var results = new List<ConversationSummaryStoreRecord>(conversations.Length);
        foreach (var conversation in conversations)
        {
            SafeMessagingIdentityRecord? identity = null;
            if (conversation.Type == ConversationType.Direct)
            {
                var otherUserId = conversation.GetDirectPair().Other(userId);
                identity = await LoadSafeIdentityAsync(otherUserId, cancellationToken);
            }
            var lastMessage = await dbContext.Messages
                .AsNoTracking()
                .Where(message => message.ConversationId == conversation.Id)
                .OrderByDescending(message => message.Sequence)
                .FirstOrDefaultAsync(cancellationToken);
            results.Add(new ConversationSummaryStoreRecord(
                conversation,
                identity,
                CreatePreview(lastMessage?.Text),
                lastMessage?.SentAtUtc,
                lastMessage?.Sequence));
        }

        return results
            .OrderByDescending(item => item.LastMessageAtUtc ?? item.Conversation.CreatedAtUtc)
            .ThenByDescending(item => item.Conversation.Id)
            .ToArray();
    }

    public async Task<ConversationMessagePageStoreResult> GetConversationMessagesAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        long? beforeSequence,
        int limit,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            throw ConversationNotFound();
        }

        var isActiveParticipant = await dbContext.ConversationParticipants.AnyAsync(
            participant => participant.ConversationId == conversationId &&
                           participant.UserId == userId &&
                           participant.Status == ConversationParticipantStatus.Active &&
                           participant.EndedAtUtc == null,
            cancellationToken);
        if (!isActiveParticipant)
        {
            throw ConversationNotFound();
        }

        var hasActiveOrganizationMembership = conversation.Scope == ConversationScope.Personal ||
            conversation.OrganizationId.HasValue &&
            accessContext.MembershipId.HasValue &&
            await dbContext.Memberships.AnyAsync(
                membership => membership.Id == accessContext.MembershipId.Value &&
                              membership.UserId == userId &&
                              membership.OrganizationId == conversation.OrganizationId.Value &&
                              membership.Status == MembershipStatus.Active &&
                              membership.EndedAtUtc == null,
                cancellationToken);
        if (!eligibilityPolicy.CanRead(new ConversationReadFacts(
                conversation.Scope,
                conversation.OrganizationId,
                accessContext,
                true,
                hasActiveOrganizationMembership)))
        {
            throw new MessagingException(
                conversation.Scope == ConversationScope.Organization
                    ? MessagingErrorCodes.OrganizationScopeMismatch
                    : MessagingErrorCodes.ConversationNotAllowed,
                "دسترسی خواندن این گفتگو در زمینه فعلی مجاز نیست.");
        }

        var descending = await dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId &&
                              (!beforeSequence.HasValue || message.Sequence < beforeSequence.Value))
            .OrderByDescending(message => message.Sequence)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        var messages = descending.Reverse().ToArray();
        long? nextBeforeSequence = null;
        if (messages.Length > 0)
        {
            var firstSequence = messages[0].Sequence;
            if (await dbContext.Messages.AnyAsync(
                    message => message.ConversationId == conversationId &&
                               message.Sequence < firstSequence,
                    cancellationToken))
            {
                nextBeforeSequence = firstSequence;
            }
        }

        return new ConversationMessagePageStoreResult(messages, nextBeforeSequence);
    }

    private static string? CreatePreview(string? text)
    {
        if (text is null || text.Length <= LastMessagePreviewLength)
        {
            return text;
        }

        return text[..LastMessagePreviewLength];
    }
}
