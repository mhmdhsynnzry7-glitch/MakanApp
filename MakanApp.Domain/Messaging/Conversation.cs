namespace MakanApp.Domain.Messaging;

public sealed class Conversation
{
    public const int TitleMaximumLength = 160;
    public const int DescriptionMaximumLength = 1000;
    public const int CreationPayloadHashLength = 32;

    private Conversation()
    {
    }

    public Guid Id { get; private set; }
    public ConversationType Type { get; private set; }
    public ConversationScope Scope { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public Guid? DirectUserLowId { get; private set; }
    public Guid? DirectUserHighId { get; private set; }
    public string? Title { get; private set; }
    public string? Description { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public ConversationManagementPolicy ManagementPolicy { get; private set; }
    public Guid? ClientOperationId { get; private set; }
    public byte[]? CreationPayloadHash { get; private set; }
    public long NextMessageSequence { get; private set; }
    public long NextChangeSequence { get; private set; }
    public ConversationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Conversation CreateDirect(
        ConversationScope scope,
        Guid? organizationId,
        Guid firstUserId,
        Guid secondUserId,
        DateTime createdAtUtc)
    {
        ValidateScope(scope, organizationId);
        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        var pair = DirectUserPair.Create(firstUserId, secondUserId);
        return new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Direct,
            Scope = scope,
            OrganizationId = organizationId,
            DirectUserLowId = pair.LowerUserId,
            DirectUserHighId = pair.HigherUserId,
            ManagementPolicy = ConversationManagementPolicy.None,
            NextMessageSequence = 1,
            NextChangeSequence = 1,
            Status = ConversationStatus.Active,
            CreatedAtUtc = createdAtUtc
        };
    }

    public static Conversation CreateUserManaged(
        ConversationType type,
        ConversationScope scope,
        Guid? organizationId,
        string title,
        string? description,
        Guid createdByUserId,
        Guid clientOperationId,
        byte[] creationPayloadHash,
        DateTime createdAtUtc)
    {
        ValidateManagedType(type);
        ValidateScope(scope, organizationId);
        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        if (createdByUserId == Guid.Empty || clientOperationId == Guid.Empty)
        {
            throw new ArgumentException("شناسه سازنده و عملیات ایجاد الزامی است.");
        }

        if (creationPayloadHash is null || creationPayloadHash.Length != CreationPayloadHashLength)
        {
            throw new ArgumentException("اثر انگشت عملیات ایجاد معتبر نیست.", nameof(creationPayloadHash));
        }

        return new Conversation
        {
            Id = Guid.NewGuid(),
            Type = type,
            Scope = scope,
            OrganizationId = organizationId,
            Title = NormalizeTitle(title),
            Description = NormalizeDescription(description),
            CreatedByUserId = createdByUserId,
            ManagementPolicy = ConversationManagementPolicy.UserManaged,
            ClientOperationId = clientOperationId,
            CreationPayloadHash = creationPayloadHash.ToArray(),
            NextMessageSequence = 1,
            NextChangeSequence = 1,
            Status = ConversationStatus.Active,
            CreatedAtUtc = createdAtUtc
        };
    }

    public static Conversation CreateSystemManagedAcademic(
        ConversationType type,
        Guid organizationId,
        string title,
        string? description,
        DateTime createdAtUtc)
    {
        ValidateManagedType(type);
        ValidateScope(ConversationScope.Organization, organizationId);
        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        return new Conversation
        {
            Id = Guid.NewGuid(),
            Type = type,
            Scope = ConversationScope.Organization,
            OrganizationId = organizationId,
            Title = NormalizeTitle(title),
            Description = NormalizeDescription(description),
            ManagementPolicy = ConversationManagementPolicy.SystemManagedAcademic,
            NextMessageSequence = 1,
            NextChangeSequence = 1,
            Status = ConversationStatus.Active,
            CreatedAtUtc = createdAtUtc
        };
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

    public long AllocateNextChangeSequence()
    {
        if (NextChangeSequence == long.MaxValue)
        {
            throw new InvalidOperationException("ظرفیت ترتیب تغییرات گفتگو به پایان رسیده است.");
        }

        var allocated = NextChangeSequence;
        NextChangeSequence++;
        return allocated;
    }

    public void Archive(DateTime archivedAtUtc)
    {
        ValidateUtc(archivedAtUtc, nameof(archivedAtUtc));
        if (Status == ConversationStatus.Archived)
        {
            return;
        }

        Status = ConversationStatus.Archived;
        ArchivedAtUtc = archivedAtUtc;
    }

    public static string NormalizeTitle(string? title)
    {
        var normalized = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > TitleMaximumLength)
        {
            throw new ArgumentException(
                $"عنوان گفتگو باید بین 1 و {TitleMaximumLength} نویسه باشد.",
                nameof(title));
        }

        return normalized;
    }

    public static string? NormalizeDescription(string? description)
    {
        var normalized = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (normalized?.Length > DescriptionMaximumLength)
        {
            throw new ArgumentException(
                $"توضیح گفتگو نمی‌تواند بیشتر از {DescriptionMaximumLength} نویسه باشد.",
                nameof(description));
        }

        return normalized;
    }

    private static void ValidateManagedType(ConversationType type)
    {
        if (type is not ConversationType.Group and not ConversationType.Channel)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private static void ValidateScope(ConversationScope scope, Guid? organizationId)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
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
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان گفتگو باید UTC باشد.", parameterName);
        }
    }
}
