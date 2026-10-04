namespace MakanApp.Domain.Assessment;

public sealed class GradeRelease
{
    private GradeRelease()
    {
    }

    private GradeRelease(
        Guid id,
        Guid organizationId,
        Guid submissionAttemptId,
        Guid evaluationRevisionId,
        Guid releasedByMembershipId,
        DateTime releasedAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        SubmissionAttemptId = submissionAttemptId;
        EvaluationRevisionId = evaluationRevisionId;
        ReleasedByMembershipId = releasedByMembershipId;
        ReleasedAtUtc = releasedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid SubmissionAttemptId { get; private set; }
    public Guid EvaluationRevisionId { get; private set; }
    public Guid ReleasedByMembershipId { get; private set; }
    public DateTime ReleasedAtUtc { get; private set; }

    public static GradeRelease Create(
        Guid organizationId,
        Guid submissionAttemptId,
        Guid evaluationRevisionId,
        Guid releasedByMembershipId,
        DateTime releasedAtUtc)
    {
        if (organizationId == Guid.Empty ||
            submissionAttemptId == Guid.Empty ||
            evaluationRevisionId == Guid.Empty ||
            releasedByMembershipId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های انتشار نمره الزامی هستند.");
        }

        if (releasedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان انتشار باید UTC باشد.", nameof(releasedAtUtc));
        }

        return new GradeRelease(
            Guid.NewGuid(),
            organizationId,
            submissionAttemptId,
            evaluationRevisionId,
            releasedByMembershipId,
            releasedAtUtc);
    }
}
