namespace MakanApp.Domain.Messaging;

public sealed class Message
{
    public const int StorageMaximumTextLength = 4000;

    private Message()
    {
    }

    private Message(
        Guid id,
        Guid conversationId,
        Guid senderParticipantId,
        Guid senderUserId,
        Guid clientMessageId,
        long sequence,
        MessageKind kind,
        string? text,
        Guid? replyToMessageId,
        Guid? forwardedFromMessageId,
        DateTime sentAtUtc)
    {
        Id = id;
        ConversationId = conversationId;
        SenderParticipantId = senderParticipantId;
        SenderUserId = senderUserId;
        ClientMessageId = clientMessageId;
        Sequence = sequence;
        Kind = kind;
        Text = text;
        SearchText = MessageSearchText.Normalize(text);
        ReplyToMessageId = replyToMessageId;
        ForwardedFromMessageId = forwardedFromMessageId;
        CurrentRevisionNumber = 1;
        SentAtUtc = sentAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderParticipantId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public Guid ClientMessageId { get; private set; }
    public long Sequence { get; private set; }
    public MessageKind Kind { get; private set; }
    public string? Text { get; private set; }
    public string? SearchText { get; private set; }
    public Guid? ReplyToMessageId { get; private set; }
    public Guid? ForwardedFromMessageId { get; private set; }
    public int CurrentRevisionNumber { get; private set; }
    public DateTime SentAtUtc { get; private set; }
    public DateTime? EditedAtUtc { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsDeleted => DeletedAtUtc.HasValue;
    public bool IsEdited => CurrentRevisionNumber > 1;

    public static Message Create(
        Guid conversationId,
        Guid senderParticipantId,
        Guid senderUserId,
        Guid clientMessageId,
        long sequence,
        MessageKind kind,
        string? text,
        int attachmentCount,
        Guid? replyToMessageId,
        Guid? forwardedFromMessageId,
        int maximumTextLength,
        DateTime sentAtUtc)
    {
        if (conversationId == Guid.Empty || senderParticipantId == Guid.Empty ||
            senderUserId == Guid.Empty || clientMessageId == Guid.Empty)
        {
            throw new ArgumentException("شناسه گفتگو، فرستنده و پیام کلاینت الزامی است.");
        }

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        if (replyToMessageId == Guid.Empty || forwardedFromMessageId == Guid.Empty)
        {
            throw new ArgumentException("شناسه مرجع پیام معتبر نیست.");
        }

        ValidateContent(kind, text, attachmentCount, maximumTextLength);
        ValidateUtc(sentAtUtc, nameof(sentAtUtc));
        return new Message(
            Guid.NewGuid(),
            conversationId,
            senderParticipantId,
            senderUserId,
            clientMessageId,
            sequence,
            kind,
            text,
            replyToMessageId,
            forwardedFromMessageId,
            sentAtUtc);
    }

    public static Message CreateText(
        Guid conversationId,
        Guid senderParticipantId,
        Guid senderUserId,
        Guid clientMessageId,
        long sequence,
        string text,
        int maximumTextLength,
        DateTime sentAtUtc) =>
        Create(
            conversationId,
            senderParticipantId,
            senderUserId,
            clientMessageId,
            sequence,
            MessageKind.Text,
            text,
            0,
            null,
            null,
            maximumTextLength,
            sentAtUtc);

    public int EditText(string? text, int maximumTextLength, DateTime editedAtUtc)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("پیام حذف‌شده قابل ویرایش نیست.");
        }

        ValidateContent(
            Kind,
            text,
            Kind == MessageKind.Text ? 0 : 1,
            maximumTextLength);
        ValidateUtc(editedAtUtc, nameof(editedAtUtc));
        if (editedAtUtc < SentAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(editedAtUtc));
        }

        if (CurrentRevisionNumber == int.MaxValue)
        {
            throw new InvalidOperationException("ظرفیت نسخه‌های پیام به پایان رسیده است.");
        }

        Text = text;
        SearchText = MessageSearchText.Normalize(text);
        EditedAtUtc = editedAtUtc;
        CurrentRevisionNumber++;
        return CurrentRevisionNumber;
    }

    public void Delete(Guid deletedByUserId, DateTime deletedAtUtc)
    {
        if (deletedByUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه حذف‌کننده الزامی است.", nameof(deletedByUserId));
        }

        ValidateUtc(deletedAtUtc, nameof(deletedAtUtc));
        if (deletedAtUtc < SentAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(deletedAtUtc));
        }

        if (IsDeleted)
        {
            throw new InvalidOperationException("پیام قبلاً حذف شده است.");
        }

        Text = null;
        SearchText = null;
        DeletedAtUtc = deletedAtUtc;
        DeletedByUserId = deletedByUserId;
    }

    public static void ValidateText(string? text, int maximumTextLength) =>
        ValidateContent(MessageKind.Text, text, 0, maximumTextLength);

    public static void ValidateContent(
        MessageKind kind,
        string? text,
        int attachmentCount,
        int maximumTextLength)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (maximumTextLength <= 0 || maximumTextLength > StorageMaximumTextLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTextLength));
        }

        if (attachmentCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attachmentCount));
        }

        if (kind == MessageKind.Text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("متن پیام الزامی است.", nameof(text));
            }

            if (attachmentCount != 0)
            {
                throw new ArgumentException("پیام متنی نباید پیوست رسانه‌ای داشته باشد.", nameof(attachmentCount));
            }
        }
        else
        {
            if (attachmentCount == 0)
            {
                throw new ArgumentException("پیام رسانه‌ای حداقل یک پیوست می‌خواهد.", nameof(attachmentCount));
            }

            if (text is not null && string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("عنوان رسانه باید خالی یا دارای متن معتبر باشد.", nameof(text));
            }
        }

        if (text?.Length > maximumTextLength)
        {
            throw new ArgumentException("متن پیام از طول مجاز بیشتر است.", nameof(text));
        }
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان پیام باید UTC باشد.", parameterName);
        }
    }
}
