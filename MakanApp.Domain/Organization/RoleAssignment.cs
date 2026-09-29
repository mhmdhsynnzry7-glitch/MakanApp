namespace MakanApp.Domain.Organization;

public sealed class RoleAssignment
{
    private RoleAssignment()
    {
    }

    private RoleAssignment(
        Guid id,
        Guid membershipId,
        OrganizationRole role,
        DateTime assignedAtUtc)
    {
        Id = id;
        MembershipId = membershipId;
        Role = role;
        Status = RoleAssignmentStatus.Active;
        AssignedAtUtc = assignedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid MembershipId { get; private set; }
    public OrganizationRole Role { get; private set; }
    public RoleAssignmentStatus Status { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive =>
        Status == RoleAssignmentStatus.Active && !EndedAtUtc.HasValue;

    public static RoleAssignment CreateActive(
        Guid membershipId,
        OrganizationRole role,
        DateTime assignedAtUtc) =>
        new(Guid.NewGuid(), membershipId, role, assignedAtUtc);

    public void End(DateTime endedAtUtc)
    {
        if (EndedAtUtc.HasValue)
        {
            return;
        }

        Status = RoleAssignmentStatus.Ended;
        EndedAtUtc = endedAtUtc;
    }
}
