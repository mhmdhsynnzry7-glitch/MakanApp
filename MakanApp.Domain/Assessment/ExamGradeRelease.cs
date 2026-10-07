namespace MakanApp.Domain.Assessment;

public sealed class ExamGradeRelease
{
    public const int RequestHashLength = 64;

    private ExamGradeRelease()
    {
    }

    private ExamGradeRelease(
        Guid id,
        Guid organizationId,
        Guid examAttemptId,
        Guid examGradeRevisionId,
        Guid releasedByMembershipId,
        Guid clientOperationId,
        string requestHash,
        DateTime releasedAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ExamAttemptId = examAttemptId;
        ExamGradeRevisionId = examGradeRevisionId;
        ReleasedByMembershipId = releasedByMembershipId;
        ClientOperationId = clientOperationId;
        RequestHash = requestHash;
        ReleasedAtUtc = releasedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ExamAttemptId { get; private set; }
    public Guid ExamGradeRevisionId { get; private set; }
    public Guid ReleasedByMembershipId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public DateTime ReleasedAtUtc { get; private set; }

    public static ExamGradeRelease Create(
        ExamGradeRevision gradeRevision,
        Guid releasedByMembershipId,
        Guid clientOperationId,
        string requestHash,
        DateTime releasedAtUtc)
    {
        if (!gradeRevision.IsReleased ||
            releasedByMembershipId == Guid.Empty ||
            clientOperationId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌ها و وضعیت انتشار نمره معتبر نیستند.");
        }

        if (requestHash is null || requestHash.Length != RequestHashLength)
        {
            throw new ArgumentException("هش درخواست انتشار معتبر نیست.", nameof(requestHash));
        }

        if (releasedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان انتشار باید UTC باشد.", nameof(releasedAtUtc));
        }

        return new ExamGradeRelease(
            Guid.NewGuid(),
            gradeRevision.OrganizationId,
            gradeRevision.ExamAttemptId,
            gradeRevision.Id,
            releasedByMembershipId,
            clientOperationId,
            requestHash,
            releasedAtUtc);
    }
}
