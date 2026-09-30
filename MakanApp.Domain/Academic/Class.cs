namespace MakanApp.Domain.Academic;

public sealed class Class
{
    private Class()
    {
    }

    private Class(
        Guid id,
        Guid organizationId,
        Guid academicPeriodId,
        Guid courseId,
        string title,
        int capacity,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        AcademicPeriodId = academicPeriodId;
        CourseId = courseId;
        Title = title;
        Capacity = capacity;
        Status = ClassStatus.Draft;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid AcademicPeriodId { get; private set; }
    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Capacity { get; private set; }
    public ClassStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ActivatedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == ClassStatus.Active && !EndedAtUtc.HasValue;

    public static Class CreateDraft(
        Guid organizationId,
        Guid academicPeriodId,
        Guid courseId,
        string title,
        int capacity,
        DateTime createdAtUtc)
    {
        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedTitle.Length > 200)
        {
            throw new ArgumentException("عنوان کلاس معتبر نیست.", nameof(title));
        }

        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "ظرفیت کلاس باید مثبت باشد.");
        }

        return new Class(
            Guid.NewGuid(),
            organizationId,
            academicPeriodId,
            courseId,
            normalizedTitle,
            capacity,
            createdAtUtc);
    }

    public void Activate(DateTime activatedAtUtc)
    {
        if (Status != ClassStatus.Draft)
        {
            throw new InvalidOperationException("فقط کلاس پیش‌نویس قابل فعال‌سازی است.");
        }

        Status = ClassStatus.Active;
        ActivatedAtUtc = activatedAtUtc;
    }

    public void Complete(DateTime completedAtUtc)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("فقط کلاس فعال قابل تکمیل است.");
        }

        Status = ClassStatus.Completed;
        EndedAtUtc = completedAtUtc;
    }

    public void Archive(DateTime archivedAtUtc)
    {
        if (Status == ClassStatus.Archived)
        {
            return;
        }

        Status = ClassStatus.Archived;
        EndedAtUtc ??= archivedAtUtc;
    }
}
