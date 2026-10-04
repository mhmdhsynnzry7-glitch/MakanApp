namespace MakanApp.Domain.Assessment;

public sealed class AssignmentVersion
{
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 10_000;

    private AssignmentVersion()
    {
    }

    private AssignmentVersion(
        Guid id,
        Guid organizationId,
        Guid classId,
        Guid assignmentId,
        int versionNumber,
        string title,
        string description,
        DateTime dueAtUtc,
        bool allowLateSubmission,
        int maxAttempts,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ClassId = classId;
        AssignmentId = assignmentId;
        VersionNumber = versionNumber;
        Title = title;
        Description = description;
        DueAtUtc = dueAtUtc;
        AllowLateSubmission = allowLateSubmission;
        MaxAttempts = maxAttempts;
        CreatedByMembershipId = createdByMembershipId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTime DueAtUtc { get; private set; }
    public bool AllowLateSubmission { get; private set; }
    public int MaxAttempts { get; private set; }
    public Guid CreatedByMembershipId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsPublished => PublishedAtUtc.HasValue;

    public static AssignmentVersion CreateDraft(
        Guid organizationId,
        Guid classId,
        Guid assignmentId,
        int versionNumber,
        string title,
        string description,
        DateTime dueAtUtc,
        bool allowLateSubmission,
        int maxAttempts,
        Guid createdByMembershipId,
        DateTime createdAtUtc)
    {
        Validate(title, description, dueAtUtc, maxAttempts, createdAtUtc);
        return new AssignmentVersion(
            Guid.NewGuid(),
            organizationId,
            classId,
            assignmentId,
            versionNumber,
            title.Trim(),
            description.Trim(),
            dueAtUtc,
            allowLateSubmission,
            maxAttempts,
            createdByMembershipId,
            createdAtUtc);
    }

    public void UpdateDraft(
        string title,
        string description,
        DateTime dueAtUtc,
        bool allowLateSubmission,
        int maxAttempts,
        DateTime nowUtc)
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("نسخه منتشرشده تکلیف قابل ویرایش نیست.");
        }

        Validate(title, description, dueAtUtc, maxAttempts, nowUtc);
        Title = title.Trim();
        Description = description.Trim();
        DueAtUtc = dueAtUtc;
        AllowLateSubmission = allowLateSubmission;
        MaxAttempts = maxAttempts;
    }

    public void Publish(DateTime publishedAtUtc)
    {
        if (IsPublished)
        {
            throw new InvalidOperationException("نسخه تکلیف قبلاً منتشر شده است.");
        }

        if (DueAtUtc <= publishedAtUtc)
        {
            throw new InvalidOperationException("مهلت تکلیف هنگام انتشار باید در آینده باشد.");
        }

        PublishedAtUtc = publishedAtUtc;
    }

    private static void Validate(
        string title,
        string description,
        DateTime dueAtUtc,
        int maxAttempts,
        DateTime nowUtc)
    {
        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedTitle.Length > MaximumTitleLength)
        {
            throw new ArgumentException("عنوان تکلیف معتبر نیست.", nameof(title));
        }

        var normalizedDescription = description?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDescription) ||
            normalizedDescription.Length > MaximumDescriptionLength)
        {
            throw new ArgumentException("توضیح تکلیف معتبر نیست.", nameof(description));
        }

        if (dueAtUtc.Kind != DateTimeKind.Utc || nowUtc.Kind != DateTimeKind.Utc || dueAtUtc <= nowUtc)
        {
            throw new ArgumentException("مهلت تکلیف باید یک زمان UTC در آینده باشد.", nameof(dueAtUtc));
        }

        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "تعداد تلاش مجاز باید بیشتر از صفر باشد.");
        }
    }
}
