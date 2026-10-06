using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Storage;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<ConversationMediaPageStoreResult> GetConversationMediaAsync(
        Guid userId,
        Guid conversationId,
        AccessContext accessContext,
        MessageKind? kind,
        long? beforeSequence,
        int limit,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        var participant = await dbContext.ConversationParticipants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.ConversationId == conversationId &&
                        item.UserId == userId &&
                        item.Status == ConversationParticipantStatus.Active &&
                        item.EndedAtUtc == null,
                cancellationToken) ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(
            conversation,
            participant,
            userId,
            accessContext,
            cancellationToken);

        var messageQuery = dbContext.Messages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId &&
                              message.DeletedAtUtc == null &&
                              (!beforeSequence.HasValue || message.Sequence < beforeSequence.Value) &&
                              (!kind.HasValue || message.Kind == kind.Value) &&
                              message.Kind != MessageKind.Text &&
                              dbContext.MessageAttachments.Any(
                                  attachment => attachment.MessageId == message.Id));
        var messages = await messageQuery
            .OrderByDescending(message => message.Sequence)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        if (messages.Length == 0)
        {
            return new ConversationMediaPageStoreResult([], null);
        }

        var messageIds = messages.Select(message => message.Id).ToArray();
        var rows = await (
            from attachment in dbContext.MessageAttachments.AsNoTracking()
            join file in dbContext.FileAssets.AsNoTracking()
                on attachment.FileAssetId equals file.Id
            where messageIds.Contains(attachment.MessageId) &&
                  file.Status == FileAssetStatus.Ready
            select new { attachment, file })
            .ToArrayAsync(cancellationToken);
        var messageMap = messages.ToDictionary(message => message.Id);
        var items = rows
            .Select(row => new ConversationMediaStoreRecord(
                messageMap[row.attachment.MessageId],
                row.attachment,
                row.file))
            .OrderBy(item => item.Message.Sequence)
            .ThenBy(item => item.Attachment.Id)
            .ToArray();
        long? nextBeforeSequence = null;
        var firstSequence = messages.Min(message => message.Sequence);
        if (await dbContext.Messages.AnyAsync(
                message => message.ConversationId == conversationId &&
                           message.DeletedAtUtc == null &&
                           message.Sequence < firstSequence &&
                           (!kind.HasValue || message.Kind == kind.Value) &&
                           message.Kind != MessageKind.Text &&
                           dbContext.MessageAttachments.Any(
                               attachment => attachment.MessageId == message.Id),
                cancellationToken))
        {
            nextBeforeSequence = firstSequence;
        }

        return new ConversationMediaPageStoreResult(items, nextBeforeSequence);
    }

    private async Task<IReadOnlyCollection<ConversationMessageStoreRecord>> LoadMessageRecordsAsync(
        IReadOnlyCollection<Message> messages,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var messageIds = messages.Select(message => message.Id).ToArray();
        var senderIds = messages.Select(message => message.SenderUserId).Distinct().ToArray();
        var identities = new Dictionary<Guid, SafeMessagingIdentityRecord>();
        foreach (var senderId in senderIds)
        {
            identities[senderId] = await LoadSafeIdentityAsync(senderId, cancellationToken);
        }

        var attachmentRows = await (
            from attachment in dbContext.MessageAttachments.AsNoTracking()
            join file in dbContext.FileAssets.AsNoTracking()
                on attachment.FileAssetId equals file.Id
            where messageIds.Contains(attachment.MessageId)
            select new { attachment, file })
            .ToArrayAsync(cancellationToken);
        var attachments = attachmentRows
            .GroupBy(row => row.attachment.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<MessageAttachmentStoreRecord>)group
                    .Select(row => new MessageAttachmentStoreRecord(row.attachment, row.file))
                    .OrderBy(row => row.Attachment.Id)
                    .ToArray());

        var reactionRows = await dbContext.MessageReactions
            .AsNoTracking()
            .Where(reaction => messageIds.Contains(reaction.MessageId) &&
                               reaction.RemovedAtUtc == null)
            .ToArrayAsync(cancellationToken);
        var reactions = reactionRows
            .GroupBy(reaction => reaction.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<MessageReactionSummaryStoreRecord>)group
                    .GroupBy(reaction => reaction.ReactionType)
                    .OrderBy(reactionGroup => reactionGroup.Key)
                    .Select(reactionGroup => new MessageReactionSummaryStoreRecord(
                        reactionGroup.Key,
                        reactionGroup.Count(),
                        reactionGroup.Any(reaction => reaction.UserId == currentUserId)))
                    .ToArray());

        var mentionRows = await dbContext.MessageMentions
            .AsNoTracking()
            .Where(mention => messageIds.Contains(mention.MessageId))
            .ToArrayAsync(cancellationToken);
        foreach (var mentionedUserId in mentionRows.Select(row => row.MentionedUserId).Distinct())
        {
            identities.TryAdd(
                mentionedUserId,
                await LoadSafeIdentityAsync(mentionedUserId, cancellationToken));
        }

        var mentions = mentionRows
            .GroupBy(mention => mention.MessageId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<SafeMessagingIdentityRecord>)group
                    .Select(mention => identities[mention.MentionedUserId])
                    .OrderBy(identity => identity.UserId)
                    .ToArray());

        var replyIds = messages
            .Where(message => message.ReplyToMessageId.HasValue)
            .Select(message => message.ReplyToMessageId!.Value)
            .Distinct()
            .ToArray();
        var replyMessages = replyIds.Length == 0
            ? new Dictionary<Guid, Message>()
            : await dbContext.Messages
                .AsNoTracking()
                .Where(message => replyIds.Contains(message.Id))
                .ToDictionaryAsync(message => message.Id, cancellationToken);
        var pinnedMessageIds = await dbContext.ConversationPins
            .AsNoTracking()
            .Where(pin => messageIds.Contains(pin.MessageId) && pin.UnpinnedAtUtc == null)
            .Select(pin => pin.MessageId)
            .ToArrayAsync(cancellationToken);
        var pinned = pinnedMessageIds.ToHashSet();

        return messages.Select(message => new ConversationMessageStoreRecord(
                message,
                identities[message.SenderUserId],
                message.ReplyToMessageId.HasValue &&
                replyMessages.TryGetValue(message.ReplyToMessageId.Value, out var reply)
                    ? new MessageReplySummaryStoreRecord(reply)
                    : null,
                reactions.GetValueOrDefault(message.Id, []),
                mentions.GetValueOrDefault(message.Id, []),
                attachments.GetValueOrDefault(message.Id, []),
                pinned.Contains(message.Id)))
            .ToArray();
    }
}
