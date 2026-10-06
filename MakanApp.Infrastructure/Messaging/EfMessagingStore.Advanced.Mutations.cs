using System.Data;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    public async Task<Message> EditMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        string? text,
        byte[] expectedVersion,
        int maximumTextLength,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var (conversation, participant, message) = await LoadMessageMutationContextAsync(
            userId,
            conversationId,
            messageId,
            accessContext,
            cancellationToken);
        EnsureMutableConversation(conversation);
        ApplyExpectedMessageVersion(message, expectedVersion);
        if (message.IsDeleted)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageDeleted,
                "پیام حذف‌شده قابل ویرایش نیست.");
        }

        if (message.SenderUserId != userId)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageNotEditable,
                "فقط فرستنده اصلی می‌تواند پیام را ویرایش کند.");
        }

        int revisionNumber;
        try
        {
            revisionNumber = message.EditText(text, maximumTextLength, nowUtc);
        }
        catch (ArgumentException exception)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageKindInvalid,
                "متن ویرایش‌شده با نوع پیام سازگار نیست.",
                exception);
        }

        dbContext.MessageRevisions.Add(MessageRevision.Create(
            message.Id,
            revisionNumber,
            message.Text,
            userId,
            nowUtc));
        await SaveAdvancedAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return message;
    }

    public async Task<Message> DeleteMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        byte[] expectedVersion,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var (conversation, participant, message) = await LoadMessageMutationContextAsync(
            userId,
            conversationId,
            messageId,
            accessContext,
            cancellationToken);
        EnsureMutableConversation(conversation);
        ApplyExpectedMessageVersion(message, expectedVersion);
        if (message.SenderUserId != userId)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageNotAllowed,
                "فقط فرستنده اصلی می‌تواند پیام را برای همه حذف کند.");
        }

        if (message.IsDeleted)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageDeleted,
                "پیام قبلاً حذف شده است.");
        }

        message.Delete(userId, nowUtc);
        await SaveAdvancedAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return message;
    }

    public async Task<MessageReaction> AddReactionAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        MessageReactionType reaction,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var (_, _, message) = await LoadMessageMutationContextAsync(
            userId,
            conversationId,
            messageId,
            accessContext,
            cancellationToken);
        if (message.IsDeleted)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageDeleted,
                "پیام حذف‌شده واکنش تازه نمی‌پذیرد.");
        }

        var existing = await dbContext.MessageReactions
            .FromSqlInterpolated($"SELECT * FROM [messaging].[MessageReactions] WITH (UPDLOCK, HOLDLOCK) WHERE [MessageId] = {messageId} AND [UserId] = {userId} AND [RemovedAtUtc] IS NULL")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing?.ReactionType == reaction)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        existing?.Remove(nowUtc);
        var created = MessageReaction.Create(messageId, userId, reaction, nowUtc);
        dbContext.MessageReactions.Add(created);
        await SaveAdvancedAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public async Task RemoveReactionAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        MessageReactionType reaction,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        await LoadMessageMutationContextAsync(
            userId,
            conversationId,
            messageId,
            accessContext,
            cancellationToken);
        var existing = await dbContext.MessageReactions
            .FromSqlInterpolated($"SELECT * FROM [messaging].[MessageReactions] WITH (UPDLOCK, HOLDLOCK) WHERE [MessageId] = {messageId} AND [UserId] = {userId} AND [RemovedAtUtc] IS NULL")
            .SingleOrDefaultAsync(cancellationToken);
        if (existing is not null && existing.ReactionType == reaction)
        {
            existing.Remove(nowUtc);
            await SaveAdvancedAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ConversationPin> PinMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var (conversation, participant, message) = await LoadMessageMutationContextAsync(
            userId,
            conversationId,
            messageId,
            accessContext,
            cancellationToken);
        EnsurePinAllowed(conversation, participant);
        if (message.IsDeleted)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageDeleted,
                "پیام حذف‌شده را نمی‌توان تازه سنجاق کرد.");
        }

        var existing = await LockActivePinAsync(conversationId, messageId, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }

        var pin = ConversationPin.Create(conversationId, messageId, userId, nowUtc);
        dbContext.ConversationPins.Add(pin);
        await SaveAdvancedAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return pin;
    }

    public async Task<ConversationPin?> UnpinMessageAsync(
        Guid userId,
        Guid conversationId,
        Guid messageId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var (conversation, participant, _) = await LoadMessageMutationContextAsync(
            userId,
            conversationId,
            messageId,
            accessContext,
            cancellationToken);
        EnsurePinAllowed(conversation, participant);
        var pin = await LockActivePinAsync(conversationId, messageId, cancellationToken);
        if (pin is not null)
        {
            pin.Unpin(userId, nowUtc);
            await SaveAdvancedAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return pin;
    }

    private async Task<(Conversation Conversation, ConversationParticipant Participant, Message Message)>
        LoadMessageMutationContextAsync(
            Guid userId,
            Guid conversationId,
            Guid messageId,
            AccessContext accessContext,
            CancellationToken cancellationToken)
    {
        var conversation = await LockConversationAsync(conversationId, cancellationToken)
            ?? throw ConversationNotFound();
        var participant = await LoadActiveParticipantAsync(conversationId, userId, cancellationToken)
            ?? throw ConversationNotFound();
        await EnsureCanReadAdvancedAsync(
            conversation,
            participant,
            userId,
            accessContext,
            cancellationToken);
        var message = await LockMessageAsync(conversationId, messageId, cancellationToken)
            ?? throw MessageNotFound();
        return (conversation, participant, message);
    }

    private void ApplyExpectedMessageVersion(Message message, byte[] expectedVersion)
    {
        if (!message.RowVersion.SequenceEqual(expectedVersion))
        {
            throw MessageConflict();
        }

        dbContext.Entry(message).Property(item => item.RowVersion).OriginalValue = expectedVersion;
    }

    private static void EnsureMutableConversation(Conversation conversation)
    {
        if (conversation.Status != ConversationStatus.Active)
        {
            throw new MessagingException(
                MessagingErrorCodes.MessageNotEditable,
                "پیام گفتگوی بایگانی‌شده قابل تغییر نیست.");
        }
    }

    private static void EnsurePinAllowed(
        Conversation conversation,
        ConversationParticipant participant)
    {
        EnsureMutableConversation(conversation);
        var allowed = conversation.Type == ConversationType.Direct ||
            conversation.ManagementPolicy == ConversationManagementPolicy.UserManaged &&
            participant.Role is ConversationParticipantRole.Owner or ConversationParticipantRole.Admin;
        if (!allowed)
        {
            throw new MessagingException(
                MessagingErrorCodes.PinNotAllowed,
                "نقش فعلی اجازه مدیریت سنجاق‌های گفتگو را ندارد.");
        }
    }

    private Task<ConversationPin?> LockActivePinAsync(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken) =>
        dbContext.ConversationPins
            .FromSqlInterpolated($"SELECT * FROM [messaging].[ConversationPins] WITH (UPDLOCK, HOLDLOCK) WHERE [ConversationId] = {conversationId} AND [MessageId] = {messageId} AND [UnpinnedAtUtc] IS NULL")
            .SingleOrDefaultAsync(cancellationToken);
}
