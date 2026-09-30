namespace MakanApp.Domain.Academic;

public sealed class Enrollment
{
    private Enrollment()
    {
    }

    private Enrollment(
        Guid id,
        Guid organizationId,
        Guid classId,
        Guid learnerOrganizationPersonId,
        DateTime enrolledAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ClassId = classId;
        LearnerOrganizationPersonId = learnerOrganizationPersonId;
        Status = EnrollmentStatus.Active;
        EnrolledAtUtc = enrolledAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid LearnerOrganizationPersonId { get; private set; }
    public EnrollmentStatus Status { get; private set; }
    public DateTime EnrolledAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == EnrollmentStatus.Active && !EndedAtUtc.HasValue;

    public static Enrollment CreateActive(
        Guid organizationId,
        Guid classId,
        Guid learnerOrganizationPersonId,
        DateTime enrolledAtUtc) =>
        new(
            Guid.NewGuid(),
            organizationId,
            classId,
            learnerOrganizationPersonId,
            enrolledAtUtc);

    public void Complete(DateTime completedAtUtc) => End(EnrollmentStatus.Completed, completedAtUtc);

    public void Withdraw(DateTime withdrawnAtUtc) => End(EnrollmentStatus.Withdrawn, withdrawnAtUtc);

    private void End(EnrollmentStatus finalStatus, DateTime endedAtUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("ثبت‌نام فعال نیست.");
        }

        Status = finalStatus;
        EndedAtUtc = endedAtUtc;
    }
}
