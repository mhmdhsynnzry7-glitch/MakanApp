using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed class MessagingService(
    IAccessContextResolver accessContextResolver,
    IMessagingStore store,
    MessagingOptions options,
    TimeProvider timeProvider) : IMessagingService
{
    public async Task<DirectConversationResult> StartOrGetDirectConversationAsync(
        Guid userId,
        Guid sessionId,
        StartDirectConversationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TargetUserId == Guid.Empty || command.TargetUserId == userId ||
            !Enum.IsDefined(command.Scope))
        {
            throw Error(
                MessagingErrorCodes.DirectRecipientNotAvailable,
                "مخاطب موردنظر برای شروع گفتگو در دسترس نیست.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var result = await store.StartOrGetDirectConversationAsync(
            userId,
            command.TargetUserId,
            command.Scope,
            context,
            UtcNow(),
            cancellationToken);
        return Map(result);
    }

    public async Task<IReadOnlyCollection<ConversationSummaryResult>> GetMyConversationsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var records = await store.GetMyConversationsAsync(userId, context, cancellationToken);
        return records.Select(Map).ToArray();
    }

    public async Task<ConversationMessagePageResult> GetConversationMessagesAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        GetConversationMessagesQuery query,
        CancellationToken cancellationToken)
    {
        ValidateConversationId(conversationId);
        var limit = ValidatePage(query.BeforeSequence, query.Limit);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var page = await store.GetConversationMessagesAsync(
            userId,
            conversationId,
            context,
            query.BeforeSequence,
            limit,
            cancellationToken);
        return new ConversationMessagePageResult(
            page.Messages.Select(Map).ToArray(),
            page.NextBeforeSequence);
    }

    public async Task<MessageReceiptResult> SendMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        SendMessageCommand command,
        CancellationToken cancellationToken)
    {
        ValidateConversationId(conversationId);
        var storeCommand = ValidateSend(command);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var message = await store.SendMessageAsync(
            userId,
            conversationId,
            context,
            storeCommand,
            options.MaximumTextLength,
            UtcNow(),
            cancellationToken);
        return ToReceipt(message);
    }

    public Task<MessageReceiptResult> SendTextMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        SendTextMessageCommand command,
        CancellationToken cancellationToken) =>
        SendMessageAsync(
            userId,
            sessionId,
            conversationId,
            new SendMessageCommand
            {
                ClientMessageId = command.ClientMessageId,
                Kind = MessageKind.Text,
                Text = command.Text
            },
            cancellationToken);

    public async Task<MessageMutationResult> EditMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        EditMessageCommand command,
        CancellationToken cancellationToken)
    {
        ValidateMessageIds(conversationId, messageId);
        if (command.Text?.Length > options.MaximumTextLength)
        {
            throw Error(MessagingErrorCodes.MessageTooLong, "متن پیام از طول مجاز بیشتر است.");
        }

        var expectedVersion = DecodeVersion(command.ExpectedVersion);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var message = await store.EditMessageAsync(
            userId,
            conversationId,
            messageId,
            context,
            command.Text,
            expectedVersion,
            options.MaximumTextLength,
            UtcNow(),
            cancellationToken);
        return ToMutation(message);
    }

    public async Task<MessageMutationResult> DeleteMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        string expectedVersion,
        CancellationToken cancellationToken)
    {
        ValidateMessageIds(conversationId, messageId);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var message = await store.DeleteMessageAsync(
            userId,
            conversationId,
            messageId,
            context,
            DecodeVersion(expectedVersion),
            UtcNow(),
            cancellationToken);
        return ToMutation(message);
    }

    public async Task<MessageReceiptResult> ForwardMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid sourceConversationId,
        Guid sourceMessageId,
        ForwardMessageCommand command,
        CancellationToken cancellationToken)
    {
        ValidateMessageIds(sourceConversationId, sourceMessageId);
        ValidateConversationId(command.DestinationConversationId);
        if (command.ClientMessageId == Guid.Empty)
        {
            throw Error(MessagingErrorCodes.MessageClientIdRequired, "شناسه پایدار پیام کلاینت الزامی است.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var message = await store.ForwardMessageAsync(
            userId,
            sourceConversationId,
            sourceMessageId,
            context,
            command.DestinationConversationId,
            command.ClientMessageId,
            options.MaximumTextLength,
            UtcNow(),
            cancellationToken);
        return ToReceipt(message);
    }

    public async Task<MessageReactionResult> AddReactionAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        AddReactionCommand command,
        CancellationToken cancellationToken)
    {
        ValidateMessageIds(conversationId, messageId);
        if (!Enum.IsDefined(command.Reaction))
        {
            throw Error(MessagingErrorCodes.ReactionNotAllowed, "واکنش انتخاب‌شده مجاز نیست.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var reaction = await store.AddReactionAsync(
            userId,
            conversationId,
            messageId,
            context,
            command.Reaction,
            UtcNow(),
            cancellationToken);
        return new MessageReactionResult(
            reaction.MessageId,
            reaction.ReactionType,
            reaction.CreatedAtUtc);
    }

    public async Task RemoveReactionAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        MessageReactionType reaction,
        CancellationToken cancellationToken)
    {
        ValidateMessageIds(conversationId, messageId);
        if (!Enum.IsDefined(reaction))
        {
            throw Error(MessagingErrorCodes.ReactionNotAllowed, "واکنش انتخاب‌شده مجاز نیست.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        await store.RemoveReactionAsync(
            userId,
            conversationId,
            messageId,
            context,
            reaction,
            UtcNow(),
            cancellationToken);
    }

    public async Task<ConversationPinResult> PinMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        ValidateMessageIds(conversationId, messageId);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        return ToPin(await store.PinMessageAsync(
            userId,
            conversationId,
            messageId,
            context,
            UtcNow(),
            cancellationToken));
    }

    public async Task<ConversationPinResult> UnpinMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        ValidateMessageIds(conversationId, messageId);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var pin = await store.UnpinMessageAsync(
            userId,
            conversationId,
            messageId,
            context,
            UtcNow(),
            cancellationToken);
        return pin is null
            ? new ConversationPinResult(conversationId, messageId, userId, UtcNow(), false)
            : ToPin(pin);
    }

    public async Task<ConversationMediaPageResult> GetConversationMediaAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        GetConversationMediaQuery query,
        CancellationToken cancellationToken)
    {
        ValidateConversationId(conversationId);
        if (query.Kind is MessageKind.Text ||
            query.Kind.HasValue && !Enum.IsDefined(query.Kind.Value))
        {
            throw Error(MessagingErrorCodes.MessageKindInvalid, "فیلتر نوع رسانه معتبر نیست.");
        }

        var limit = ValidatePage(query.BeforeSequence, query.Limit);
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var page = await store.GetConversationMediaAsync(
            userId,
            conversationId,
            context,
            query.Kind,
            query.BeforeSequence,
            limit,
            cancellationToken);
        return new ConversationMediaPageResult(
            page.Items.Select(item => new ConversationMediaItemResult(
                item.Message.Id,
                item.Message.Sequence,
                item.Message.SenderUserId,
                item.Message.SentAtUtc,
                Map(item.Attachment, item.FileAsset))).ToArray(),
            page.NextBeforeSequence);
    }

    private SendMessageStoreCommand ValidateSend(SendMessageCommand command)
    {
        if (command.ClientMessageId == Guid.Empty)
        {
            throw Error(MessagingErrorCodes.MessageClientIdRequired, "شناسه پایدار پیام کلاینت الزامی است.");
        }

        if (!Enum.IsDefined(command.Kind))
        {
            throw Error(MessagingErrorCodes.MessageKindInvalid, "نوع پیام معتبر نیست.");
        }

        var attachmentIds = (command.AttachmentIds ?? []).Distinct().ToArray();
        if (attachmentIds.Any(id => id == Guid.Empty) ||
            attachmentIds.Length > options.MaximumAttachmentsPerMessage)
        {
            throw Error(MessagingErrorCodes.MessageAttachmentNotAllowed, "فهرست پیوست‌های پیام معتبر نیست.");
        }

        var mentionedUserIds = (command.MentionedUserIds ?? []).Distinct().ToArray();
        if (mentionedUserIds.Any(id => id == Guid.Empty) ||
            mentionedUserIds.Length > options.MaximumMentionsPerMessage)
        {
            throw Error(MessagingErrorCodes.MentionNotAllowed, "فهرست منشن‌های پیام معتبر نیست.");
        }

        if (command.Text?.Length > options.MaximumTextLength)
        {
            throw Error(MessagingErrorCodes.MessageTooLong, "متن پیام از طول مجاز بیشتر است.");
        }

        if (command.Kind == MessageKind.Text)
        {
            if (string.IsNullOrWhiteSpace(command.Text))
            {
                throw Error(MessagingErrorCodes.MessageEmpty, "متن پیام الزامی است.");
            }

            if (attachmentIds.Length > 0)
            {
                throw Error(MessagingErrorCodes.MessageKindInvalid, "پیام متنی نمی‌تواند پیوست رسانه‌ای داشته باشد.");
            }
        }
        else if (attachmentIds.Length == 0)
        {
            throw Error(MessagingErrorCodes.MessageAttachmentRequired, "این نوع پیام حداقل یک پیوست می‌خواهد.");
        }
        else if (command.Text is not null && string.IsNullOrWhiteSpace(command.Text))
        {
            throw Error(MessagingErrorCodes.MessageKindInvalid, "عنوان رسانه باید خالی یا دارای متن معتبر باشد.");
        }

        return new SendMessageStoreCommand(
            command.ClientMessageId,
            command.Kind,
            command.Text,
            attachmentIds,
            command.ReplyToMessageId,
            mentionedUserIds);
    }

    private int ValidatePage(long? beforeSequence, int? requestedLimit)
    {
        if (beforeSequence is <= 0)
        {
            throw Error(MessagingErrorCodes.MessagePageInvalid, "نشانگر ترتیب پیام معتبر نیست.");
        }

        var limit = requestedLimit ?? options.DefaultHistoryLimit;
        if (limit <= 0 || limit > options.MaximumHistoryLimit)
        {
            throw Error(
                MessagingErrorCodes.MessagePageInvalid,
                $"تعداد اقلام هر صفحه باید بین 1 و {options.MaximumHistoryLimit} باشد.");
        }

        return limit;
    }

    private static byte[] DecodeVersion(string value)
    {
        try
        {
            var version = Convert.FromBase64String(value);
            return version.Length == 8 ? version : throw new FormatException();
        }
        catch (FormatException)
        {
            throw Error(MessagingErrorCodes.MessageEditConflict, "نسخه پیام معتبر نیست.");
        }
    }

    private static ConversationMessageResult Map(ConversationMessageStoreRecord record)
    {
        var message = record.Message;
        var isDeleted = message.IsDeleted;
        return new ConversationMessageResult(
            message.Id,
            message.ConversationId,
            message.SenderUserId,
            Map(record.Sender),
            message.ClientMessageId,
            message.Sequence,
            isDeleted ? null : message.Kind,
            isDeleted ? null : message.Text,
            message.SentAtUtc,
            message.EditedAtUtc,
            message.DeletedAtUtc,
            message.IsEdited,
            isDeleted,
            isDeleted || record.Reply is null ? null : MapReply(record.Reply.Message),
            !isDeleted && message.ForwardedFromMessageId.HasValue,
            isDeleted
                ? []
                : record.Reactions.Select(item => new MessageReactionSummaryResult(
                    item.Reaction,
                    item.Count,
                    item.ReactedByCurrentUser)).ToArray(),
            isDeleted ? [] : record.Mentions.Select(Map).ToArray(),
            isDeleted
                ? []
                : record.Attachments.Select(item => Map(item.Attachment, item.FileAsset)).ToArray(),
            record.IsPinned,
            Convert.ToBase64String(message.RowVersion));
    }

    private static MessageReplySummaryResult MapReply(Message source) =>
        new(
            source.Id,
            source.SenderUserId,
            source.IsDeleted ? null : source.Kind,
            source.IsDeleted ? null : source.Text,
            source.IsDeleted);

    private static MessageAttachmentResult Map(
        MessageAttachment attachment,
        Domain.Storage.FileAsset fileAsset) =>
        new(
            fileAsset.Id,
            attachment.Kind,
            fileAsset.OriginalFileName,
            fileAsset.ContentType,
            fileAsset.SizeBytes,
            attachment.CreatedAtUtc);

    private static DirectConversationResult Map(DirectConversationStoreResult result) =>
        new(
            result.Conversation.Id,
            result.Conversation.Type,
            result.Conversation.Scope,
            result.Conversation.OrganizationId,
            Map(result.OtherParticipant),
            result.Conversation.CreatedAtUtc,
            result.AlreadyExisted);

    private static ConversationSummaryResult Map(ConversationSummaryStoreRecord result) =>
        new(
            result.Conversation.Id,
            result.Conversation.Type,
            result.Conversation.Scope,
            result.Conversation.OrganizationId,
            result.Conversation.Title,
            result.OtherParticipant is null ? null : Map(result.OtherParticipant),
            result.LastMessagePreview,
            result.LastMessageAtUtc,
            result.LastMessageSequence);

    private static SafeMessagingIdentityResult Map(SafeMessagingIdentityRecord result) =>
        new(result.UserId, result.Username, result.DisplayName);

    private static MessageReceiptResult ToReceipt(Message message) =>
        new(
            message.Id,
            message.ConversationId,
            message.ClientMessageId,
            message.Sequence,
            message.Kind,
            message.SentAtUtc,
            MessageReceiptStatus.Sent,
            Convert.ToBase64String(message.RowVersion));

    private static MessageMutationResult ToMutation(Message message) =>
        new(
            message.Id,
            message.Sequence,
            message.EditedAtUtc,
            message.DeletedAtUtc,
            message.IsDeleted,
            Convert.ToBase64String(message.RowVersion));

    private static ConversationPinResult ToPin(ConversationPin pin) =>
        new(
            pin.ConversationId,
            pin.MessageId,
            pin.PinnedByUserId,
            pin.PinnedAtUtc,
            pin.IsActive);

    private static void ValidateConversationId(Guid conversationId)
    {
        if (conversationId == Guid.Empty)
        {
            throw Error(MessagingErrorCodes.ConversationNotFound, "گفتگو یافت نشد.");
        }
    }

    private static void ValidateMessageIds(Guid conversationId, Guid messageId)
    {
        ValidateConversationId(conversationId);
        if (messageId == Guid.Empty)
        {
            throw Error(MessagingErrorCodes.MessageNotFound, "پیام یافت نشد.");
        }
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static MessagingException Error(string code, string message) => new(code, message);
}
