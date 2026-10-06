namespace MakanApp.Domain.Assessment;

public sealed class Exam
{
    private Exam()
    {
    }

    private Exam(
        Guid id,
        Guid organizationId,
        Guid classId,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ClassId = classId;
        CreatedByMembershipId = createdByMembershipId;
        CurrentVersionNumber = 1;
        Status = ExamStatus.Draft;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid CreatedByMembershipId { get; private set; }
    public int CurrentVersionNumber { get; private set; }
    public int? LatestPublishedVersionNumber { get; private set; }
    public ExamStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Exam CreateDraft(
        Guid organizationId,
        Guid classId,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        if (organizationId == Guid.Empty || classId == Guid.Empty || createdByMembershipId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های آزمون الزامی هستند.");
        }

        return new Exam(
            Guid.NewGuid(),
            organizationId,
            classId,
            createdByMembershipId,
            createdAtUtc);
    }

    public void PublishCurrentVersion(int versionNumber)
    {
        if (versionNumber != CurrentVersionNumber)
        {
            throw new InvalidOperationException("فقط نسخه جاری آزمون قابل انتشار است.");
        }

        if (LatestPublishedVersionNumber == versionNumber)
        {
            throw new InvalidOperationException("نسخه جاری آزمون قبلاً منتشر شده است.");
        }

        LatestPublishedVersionNumber = versionNumber;
        Status = ExamStatus.Published;
    }

    public int StartNextDraft(int publishedVersionNumber)
    {
        if (LatestPublishedVersionNumber != publishedVersionNumber ||
            CurrentVersionNumber != publishedVersionNumber)
        {
            throw new InvalidOperationException("نسخه جاری باید پیش از ساخت نسخه بعدی منتشر شده باشد.");
        }

        CurrentVersionNumber++;
        return CurrentVersionNumber;
    }

    private static DateTime EnsureUtc(DateTime value, string parameterName) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new ArgumentException("زمان باید UTC باشد.", parameterName);
}
