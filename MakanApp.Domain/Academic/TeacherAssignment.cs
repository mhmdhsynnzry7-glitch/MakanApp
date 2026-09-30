namespace MakanApp.Domain.Academic;

public sealed class TeacherAssignment
{
    private TeacherAssignment()
    {
    }

    private TeacherAssignment(
        Guid id,
        Guid organizationId,
        Guid classId,
        Guid teacherMembershipId,
        DateTime assignedAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ClassId = classId;
        TeacherMembershipId = teacherMembershipId;
        Status = TeacherAssignmentStatus.Active;
        AssignedAtUtc = assignedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid TeacherMembershipId { get; private set; }
    public TeacherAssignmentStatus Status { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == TeacherAssignmentStatus.Active && !EndedAtUtc.HasValue;

    public static TeacherAssignment CreateActive(
        Guid organizationId,
        Guid classId,
        Guid teacherMembershipId,
        DateTime assignedAtUtc) =>
        new(Guid.NewGuid(), organizationId, classId, teacherMembershipId, assignedAtUtc);

    public bool GrantsAccessTo(Guid classId) => IsActive && ClassId == classId;

    public void End(DateTime endedAtUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("انتساب معلم فعال نیست.");
        }

        Status = TeacherAssignmentStatus.Ended;
        EndedAtUtc = endedAtUtc;
    }
}
