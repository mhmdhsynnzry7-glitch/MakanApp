namespace MakanApp.Domain.Messaging;

public sealed class MessageRevision
{
    private MessageRevision()
    {
    }

    private MessageRevision(
        Guid id,
        Guid messageId,
        int revisionNumber,
        string? text,
        Guid authoredByUserId,
        DateTime createdAtUtc)
    {
        Id = id;
        MessageId = messageId;
        RevisionNumber = revisionNumber;
        Text = text;
        AuthoredByUserId = authoredByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public int RevisionNumber { get; private set; }
    public string? Text { get; private set; }
    public Guid AuthoredByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static MessageRevision Create(
        Guid messageId,
        int revisionNumber,
        string? text,
        Guid authoredByUserId,
        DateTime createdAtUtc)
    {
        if (messageId == Guid.Empty || authoredByUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه پیام و نویسنده نسخه الزامی است.");
        }

        if (revisionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        }

        if (text?.Length > Message.StorageMaximumTextLength)
        {
            throw new ArgumentException("متن نسخه از طول ذخیره‌سازی بیشتر است.", nameof(text));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان نسخه باید UTC باشد.", nameof(createdAtUtc));
        }

        return new MessageRevision(
            Guid.NewGuid(),
            messageId,
            revisionNumber,
            text,
            authoredByUserId,
            createdAtUtc);
    }
}
