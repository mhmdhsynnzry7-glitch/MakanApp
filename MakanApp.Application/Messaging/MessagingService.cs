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
            timeProvider.GetUtcNow().UtcDateTime,
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
        if (conversationId == Guid.Empty)
        {
            throw ConversationNotFound();
        }

        if (query.BeforeSequence is <= 0)
        {
            throw Error(
                MessagingErrorCodes.MessagePageInvalid,
                "نشانگر ترتیب پیام معتبر نیست.");
        }

        var limit = query.Limit ?? options.DefaultHistoryLimit;
        if (limit <= 0 || limit > options.MaximumHistoryLimit)
        {
            throw Error(
                MessagingErrorCodes.MessagePageInvalid,
                $"تعداد پیام‌های هر صفحه باید بین 1 و {options.MaximumHistoryLimit} باشد.");
        }

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

    public async Task<MessageReceiptResult> SendTextMessageAsync(
        Guid userId,
        Guid sessionId,
        Guid conversationId,
        SendTextMessageCommand command,
        CancellationToken cancellationToken)
    {
        if (conversationId == Guid.Empty)
        {
            throw ConversationNotFound();
        }

        if (command.ClientMessageId == Guid.Empty)
        {
            throw Error(
                MessagingErrorCodes.MessageClientIdRequired,
                "شناسه پایدار پیام کلاینت الزامی است.");
        }

        if (string.IsNullOrWhiteSpace(command.Text))
        {
            throw Error(MessagingErrorCodes.MessageEmpty, "متن پیام الزامی است.");
        }

        if (command.Text.Length > options.MaximumTextLength)
        {
            throw Error(
                MessagingErrorCodes.MessageTooLong,
                $"متن پیام نمی‌تواند بیشتر از {options.MaximumTextLength} نویسه باشد.");
        }

        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        var message = await store.SendTextMessageAsync(
            userId,
            conversationId,
            context,
            command.ClientMessageId,
            command.Text,
            options.MaximumTextLength,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        return new MessageReceiptResult(
            message.Id,
            message.ConversationId,
            message.ClientMessageId,
            message.Sequence,
            message.SentAtUtc,
            MessageReceiptStatus.Sent);
    }

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

    private static ConversationMessageResult Map(Message message) =>
        new(
            message.Id,
            message.ConversationId,
            message.SenderUserId,
            message.ClientMessageId,
            message.Sequence,
            message.Kind,
            message.Text,
            message.SentAtUtc);

    private static MessagingException ConversationNotFound() =>
        Error(MessagingErrorCodes.ConversationNotFound, "گفتگو یافت نشد.");

    private static MessagingException Error(string code, string message) => new(code, message);
}
