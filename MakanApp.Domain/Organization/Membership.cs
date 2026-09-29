namespace MakanApp.Domain.Organization;

public sealed class Membership
{
    private Membership()
    {
    }

    private Membership(
        Guid id,
        Guid userId,
        Guid organizationId,
        DateTime createdAtUtc,
        DateTime activatedAtUtc)
    {
        Id = id;
        UserId = userId;
        OrganizationId = organizationId;
        Status = MembershipStatus.Active;
        CreatedAtUtc = createdAtUtc;
        ActivatedAtUtc = activatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public MembershipStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ActivatedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive =>
        Status == MembershipStatus.Active && !EndedAtUtc.HasValue;

    public static Membership CreateActive(
        Guid userId,
        Guid organizationId,
        DateTime createdAtUtc) =>
        new(Guid.NewGuid(), userId, organizationId, createdAtUtc, createdAtUtc);

    public void Suspend()
    {
        if (!EndedAtUtc.HasValue)
        {
            Status = MembershipStatus.Suspended;
        }
    }

    public void End(DateTime endedAtUtc)
    {
        if (EndedAtUtc.HasValue)
        {
            return;
        }

        Status = MembershipStatus.Ended;
        EndedAtUtc = endedAtUtc;
    }
}
