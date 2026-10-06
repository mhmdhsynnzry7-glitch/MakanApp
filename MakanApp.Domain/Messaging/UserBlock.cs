namespace MakanApp.Domain.Messaging;

public sealed class UserBlock
{
    private UserBlock()
    {
    }

    private UserBlock(
        Guid id,
        Guid blockerUserId,
        Guid blockedUserId,
        DateTime createdAtUtc)
    {
        Id = id;
        BlockerUserId = blockerUserId;
        BlockedUserId = blockedUserId;
        Status = UserBlockStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid BlockerUserId { get; private set; }
    public Guid BlockedUserId { get; private set; }
    public UserBlockStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == UserBlockStatus.Active && !EndedAtUtc.HasValue;

    public static UserBlock Create(
        Guid blockerUserId,
        Guid blockedUserId,
        DateTime createdAtUtc)
    {
        if (blockerUserId == Guid.Empty || blockedUserId == Guid.Empty ||
            blockerUserId == blockedUserId)
        {
            throw new ArgumentException("شناسه مسدودکننده و کاربر مسدودشده باید معتبر و متفاوت باشند.");
        }

        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        return new UserBlock(Guid.NewGuid(), blockerUserId, blockedUserId, createdAtUtc);
    }

    public void End(DateTime endedAtUtc)
    {
        ValidateUtc(endedAtUtc, nameof(endedAtUtc));
        if (!IsActive)
        {
            return;
        }

        if (endedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(endedAtUtc));
        }

        Status = UserBlockStatus.Ended;
        EndedAtUtc = endedAtUtc;
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان مسدودسازی باید UTC باشد.", parameterName);
        }
    }
}
