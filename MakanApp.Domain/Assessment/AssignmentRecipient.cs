namespace MakanApp.Domain.Assessment;

public sealed class AssignmentRecipient
{
    private AssignmentRecipient()
    {
    }

    private AssignmentRecipient(
        Guid id,
        Guid organizationId,
        Guid classId,
        Guid assignmentId,
        Guid assignmentVersionId,
        Guid enrollmentId,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ClassId = classId;
        AssignmentId = assignmentId;
        AssignmentVersionId = assignmentVersionId;
        EnrollmentId = enrollmentId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public Guid AssignmentVersionId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static AssignmentRecipient Create(
        Guid organizationId,
        Guid classId,
        Guid assignmentId,
        Guid assignmentVersionId,
        Guid enrollmentId,
        DateTime createdAtUtc) =>
        new(
            Guid.NewGuid(),
            organizationId,
            classId,
            assignmentId,
            assignmentVersionId,
            enrollmentId,
            createdAtUtc);
}
