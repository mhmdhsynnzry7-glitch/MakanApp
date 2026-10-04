namespace MakanApp.Domain.Messaging;

public sealed class Conversation
{
    private Conversation()
    {
    }

    private Conversation(
        Guid id,
        ConversationScope scope,
        Guid? organizationId,
        DirectUserPair pair,
        DateTime createdAtUtc)
    {
        Id = id;
        Type = ConversationType.Direct;
        Scope = scope;
        OrganizationId = organizationId;
        DirectUserLowId = pair.LowerUserId;
        DirectUserHighId = pair.HigherUserId;
        NextMessageSequence = 1;
        Status = ConversationStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public ConversationType Type { get; private set; }
    public ConversationScope Scope { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? DirectUserLowId { get; private set; }
    public Guid? DirectUserHighId { get; private set; }
    public long NextMessageSequence { get; private set; }
    public ConversationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Conversation CreateDirect(
        ConversationScope scope,
        Guid? organizationId,
        Guid firstUserId,
        Guid secondUserId,
        DateTime createdAtUtc)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان ایجاد گفتگو باید UTC باشد.", nameof(createdAtUtc));
        }

        if (scope == ConversationScope.Personal && organizationId.HasValue)
        {
            throw new ArgumentException("گفتگوی شخصی نباید سازمان داشته باشد.", nameof(organizationId));
        }

        if (scope == ConversationScope.Organization &&
            (!organizationId.HasValue || organizationId.Value == Guid.Empty))
        {
            throw new ArgumentException("گفتگوی سازمانی باید سازمان معتبر داشته باشد.", nameof(organizationId));
        }

        return new Conversation(
            Guid.NewGuid(),
            scope,
            organizationId,
            DirectUserPair.Create(firstUserId, secondUserId),
            createdAtUtc);
    }

    public DirectUserPair GetDirectPair()
    {
        if (Type != ConversationType.Direct ||
            !DirectUserLowId.HasValue ||
            !DirectUserHighId.HasValue)
        {
            throw new InvalidOperationException("گفتگو زوج مستقیم معتبر ندارد.");
        }

        return new DirectUserPair(DirectUserLowId.Value, DirectUserHighId.Value);
    }

    public long AllocateNextMessageSequence()
    {
        if (Status != ConversationStatus.Active)
        {
            throw new InvalidOperationException("گفتگوی غیرفعال پیام تازه نمی‌پذیرد.");
        }

        if (NextMessageSequence == long.MaxValue)
        {
            throw new InvalidOperationException("ظرفیت ترتیب پیام گفتگو به پایان رسیده است.");
        }

        var allocated = NextMessageSequence;
        NextMessageSequence++;
        return allocated;
    }
}
