namespace MakanApp.Domain.Messaging;

public sealed class MessageAttachment
{
    private MessageAttachment()
    {
    }

    private MessageAttachment(
        Guid id,
        Guid messageId,
        Guid fileAssetId,
        MessageKind kind,
        DateTime createdAtUtc)
    {
        Id = id;
        MessageId = messageId;
        FileAssetId = fileAssetId;
        Kind = kind;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid FileAssetId { get; private set; }
    public MessageKind Kind { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static MessageAttachment Create(
        Guid messageId,
        Guid fileAssetId,
        MessageKind kind,
        DateTime createdAtUtc)
    {
        if (messageId == Guid.Empty || fileAssetId == Guid.Empty)
        {
            throw new ArgumentException("شناسه پیام و فایل الزامی است.");
        }

        if (kind is MessageKind.Text || !Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان اتصال فایل باید UTC باشد.", nameof(createdAtUtc));
        }

        return new MessageAttachment(Guid.NewGuid(), messageId, fileAssetId, kind, createdAtUtc);
    }
}
