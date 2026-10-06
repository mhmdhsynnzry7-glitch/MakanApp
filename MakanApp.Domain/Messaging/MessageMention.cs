namespace MakanApp.Domain.Messaging;

public sealed class MessageMention
{
    private MessageMention()
    {
    }

    private MessageMention(Guid id, Guid messageId, Guid mentionedUserId, DateTime createdAtUtc)
    {
        Id = id;
        MessageId = messageId;
        MentionedUserId = mentionedUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid MentionedUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static MessageMention Create(Guid messageId, Guid mentionedUserId, DateTime createdAtUtc)
    {
        if (messageId == Guid.Empty || mentionedUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه پیام و کاربر منشن‌شده الزامی است.");
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان منشن باید UTC باشد.", nameof(createdAtUtc));
        }

        return new MessageMention(Guid.NewGuid(), messageId, mentionedUserId, createdAtUtc);
    }
}
