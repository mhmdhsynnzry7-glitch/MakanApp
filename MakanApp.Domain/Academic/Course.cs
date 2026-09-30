namespace MakanApp.Domain.Academic;

public sealed class Course
{
    private Course()
    {
    }

    private Course(Guid id, Guid organizationId, string title, DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        Title = title;
        Status = CourseStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public CourseStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == CourseStatus.Active;

    public static Course Create(Guid organizationId, string title, DateTime createdAtUtc)
    {
        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedTitle.Length > 200)
        {
            throw new ArgumentException("عنوان درس معتبر نیست.", nameof(title));
        }

        return new Course(Guid.NewGuid(), organizationId, normalizedTitle, createdAtUtc);
    }

    public void Archive() => Status = CourseStatus.Archived;
}
