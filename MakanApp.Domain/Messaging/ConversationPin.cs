namespace MakanApp.Domain.Messaging;

public sealed class ConversationPin
{
    private ConversationPin()
    {
    }

    private ConversationPin(
        Guid id,
        Guid conversationId,
        Guid messageId,
        Guid pinnedByUserId,
        DateTime pinnedAtUtc)
    {
        Id = id;
        ConversationId = conversationId;
        MessageId = messageId;
        PinnedByUserId = pinnedByUserId;
        PinnedAtUtc = pinnedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid PinnedByUserId { get; private set; }
    public DateTime PinnedAtUtc { get; private set; }
    public Guid? UnpinnedByUserId { get; private set; }
    public DateTime? UnpinnedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => !UnpinnedAtUtc.HasValue;

    public static ConversationPin Create(
        Guid conversationId,
        Guid messageId,
        Guid pinnedByUserId,
        DateTime pinnedAtUtc)
    {
        if (conversationId == Guid.Empty || messageId == Guid.Empty || pinnedByUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه گفتگو، پیام و سنجاق‌کننده الزامی است.");
        }

        ValidateUtc(pinnedAtUtc, nameof(pinnedAtUtc));
        return new ConversationPin(
            Guid.NewGuid(),
            conversationId,
            messageId,
            pinnedByUserId,
            pinnedAtUtc);
    }

    public void Unpin(Guid unpinnedByUserId, DateTime unpinnedAtUtc)
    {
        if (unpinnedByUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه بردارنده سنجاق الزامی است.", nameof(unpinnedByUserId));
        }

        ValidateUtc(unpinnedAtUtc, nameof(unpinnedAtUtc));
        if (IsActive)
        {
            UnpinnedByUserId = unpinnedByUserId;
            UnpinnedAtUtc = unpinnedAtUtc;
        }
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان سنجاق باید UTC باشد.", parameterName);
        }
    }
}
