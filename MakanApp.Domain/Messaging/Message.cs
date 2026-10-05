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
        string text,
        DateTime sentAtUtc)
    {
        Id = id;
        ConversationId = conversationId;
        SenderParticipantId = senderParticipantId;
        SenderUserId = senderUserId;
        ClientMessageId = clientMessageId;
        Sequence = sequence;
        Kind = MessageKind.Text;
        Text = text;
        SentAtUtc = sentAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderParticipantId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public Guid ClientMessageId { get; private set; }
    public long Sequence { get; private set; }
    public MessageKind Kind { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTime SentAtUtc { get; private set; }

    public static Message CreateText(
        Guid conversationId,
        Guid senderParticipantId,
        Guid senderUserId,
        Guid clientMessageId,
        long sequence,
        string text,
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

        ValidateText(text, maximumTextLength);
        if (sentAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان پذیرش پیام باید UTC باشد.", nameof(sentAtUtc));
        }

        return new Message(
            Guid.NewGuid(),
            conversationId,
            senderParticipantId,
            senderUserId,
            clientMessageId,
            sequence,
            text,
            sentAtUtc);
    }

    public static void ValidateText(string? text, int maximumTextLength)
    {
        if (maximumTextLength <= 0 || maximumTextLength > StorageMaximumTextLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumTextLength));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("متن پیام الزامی است.", nameof(text));
        }

        if (text.Length > maximumTextLength)
        {
            throw new ArgumentException("متن پیام از طول مجاز بیشتر است.", nameof(text));
        }
    }
}
