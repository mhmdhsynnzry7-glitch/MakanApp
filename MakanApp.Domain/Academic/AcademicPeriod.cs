namespace MakanApp.Domain.Academic;

public sealed class AcademicPeriod
{
    private AcademicPeriod()
    {
    }

    private AcademicPeriod(
        Guid id,
        Guid organizationId,
        string title,
        DateOnly startDate,
        DateOnly endDate,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        Title = title;
        StartDate = startDate;
        EndDate = endDate;
        Status = AcademicPeriodStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public AcademicPeriodStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == AcademicPeriodStatus.Active;

    public static AcademicPeriod Create(
        Guid organizationId,
        string title,
        DateOnly startDate,
        DateOnly endDate,
        DateTime createdAtUtc)
    {
        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedTitle.Length > 200)
        {
            throw new ArgumentException("عنوان دوره تحصیلی معتبر نیست.", nameof(title));
        }

        if (endDate <= startDate)
        {
            throw new ArgumentException("تاریخ پایان باید بعد از تاریخ شروع باشد.", nameof(endDate));
        }

        return new AcademicPeriod(
            Guid.NewGuid(),
            organizationId,
            normalizedTitle,
            startDate,
            endDate,
            createdAtUtc);
    }

    public void Close() => Status = AcademicPeriodStatus.Closed;
}
