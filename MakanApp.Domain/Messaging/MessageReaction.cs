namespace MakanApp.Domain.Messaging;

public sealed class MessageReaction
{
    private MessageReaction()
    {
    }

    private MessageReaction(
        Guid id,
        Guid messageId,
        Guid userId,
        MessageReactionType reactionType,
        DateTime createdAtUtc)
    {
        Id = id;
        MessageId = messageId;
        UserId = userId;
        ReactionType = reactionType;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid UserId { get; private set; }
    public MessageReactionType ReactionType { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RemovedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => !RemovedAtUtc.HasValue;

    public static MessageReaction Create(
        Guid messageId,
        Guid userId,
        MessageReactionType reactionType,
        DateTime createdAtUtc)
    {
        if (messageId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("شناسه پیام و واکنش‌دهنده الزامی است.");
        }

        if (!Enum.IsDefined(reactionType))
        {
            throw new ArgumentOutOfRangeException(nameof(reactionType));
        }

        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        return new MessageReaction(Guid.NewGuid(), messageId, userId, reactionType, createdAtUtc);
    }

    public void Remove(DateTime removedAtUtc)
    {
        ValidateUtc(removedAtUtc, nameof(removedAtUtc));
        RemovedAtUtc ??= removedAtUtc;
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان واکنش باید UTC باشد.", parameterName);
        }
    }
}
