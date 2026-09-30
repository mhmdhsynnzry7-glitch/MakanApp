namespace MakanApp.Domain.Academic;

public sealed class Session
{
    private Session()
    {
    }

    private Session(
        Guid id,
        Guid organizationId,
        Guid classId,
        Guid? scheduleRuleId,
        DateOnly? occurrenceLocalDate,
        string title,
        DateTime startUtc,
        DateTime endUtc,
        string timeZoneId,
        string? meetingUrl,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ClassId = classId;
        ScheduleRuleId = scheduleRuleId;
        OccurrenceLocalDate = occurrenceLocalDate;
        Title = title;
        StartUtc = startUtc;
        EndUtc = endUtc;
        TimeZoneId = timeZoneId;
        MeetingUrl = meetingUrl;
        Status = SessionStatus.Scheduled;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid? ScheduleRuleId { get; private set; }
    public DateOnly? OccurrenceLocalDate { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public string? MeetingUrl { get; private set; }
    public SessionStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsScheduled => Status == SessionStatus.Scheduled;

    public static Session CreateScheduled(
        Guid organizationId,
        Guid classId,
        string title,
        DateTime startUtc,
        DateTime endUtc,
        string timeZoneId,
        string? meetingUrl,
        DateTime createdAtUtc,
        Guid? scheduleRuleId = null,
        DateOnly? occurrenceLocalDate = null)
    {
        Validate(title, startUtc, endUtc, timeZoneId, meetingUrl);
        return new Session(
            Guid.NewGuid(),
            organizationId,
            classId,
            scheduleRuleId,
            occurrenceLocalDate,
            title.Trim(),
            startUtc,
            endUtc,
            timeZoneId.Trim(),
            NormalizeMeetingUrl(meetingUrl),
            createdAtUtc);
    }

    public void Update(
        string title,
        DateTime startUtc,
        DateTime endUtc,
        string timeZoneId,
        string? meetingUrl,
        DateTime nowUtc)
    {
        if (!IsScheduled || StartUtc <= nowUtc)
        {
            throw new InvalidOperationException("فقط جلسه برنامه‌ریزی‌شده آینده قابل ویرایش است.");
        }

        Validate(title, startUtc, endUtc, timeZoneId, meetingUrl);
        Title = title.Trim();
        StartUtc = startUtc;
        EndUtc = endUtc;
        TimeZoneId = timeZoneId.Trim();
        MeetingUrl = NormalizeMeetingUrl(meetingUrl);
    }

    public void Cancel(DateTime cancelledAtUtc)
    {
        if (!IsScheduled)
        {
            throw new InvalidOperationException("فقط جلسه برنامه‌ریزی‌شده قابل لغو است.");
        }

        Status = SessionStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
    }

    public void Complete(DateTime completedAtUtc)
    {
        if (!IsScheduled)
        {
            throw new InvalidOperationException("فقط جلسه برنامه‌ریزی‌شده قابل تکمیل است.");
        }

        Status = SessionStatus.Completed;
        CompletedAtUtc = completedAtUtc;
    }

    private static void Validate(
        string title,
        DateTime startUtc,
        DateTime endUtc,
        string timeZoneId,
        string? meetingUrl)
    {
        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedTitle.Length > 200)
        {
            throw new ArgumentException("عنوان جلسه معتبر نیست.", nameof(title));
        }

        if (startUtc.Kind != DateTimeKind.Utc || endUtc.Kind != DateTimeKind.Utc || endUtc <= startUtc)
        {
            throw new ArgumentException("زمان پایان جلسه باید بعد از زمان شروع و هر دو UTC باشند.");
        }

        var normalizedTimeZoneId = timeZoneId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTimeZoneId) || normalizedTimeZoneId.Length > 100)
        {
            throw new ArgumentException("منطقه زمانی جلسه معتبر نیست.", nameof(timeZoneId));
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(normalizedTimeZoneId);

        if (!string.IsNullOrWhiteSpace(meetingUrl) &&
            (!Uri.TryCreate(meetingUrl.Trim(), UriKind.Absolute, out var uri) ||
             uri.Scheme != Uri.UriSchemeHttps ||
             meetingUrl.Trim().Length > 1000))
        {
            throw new ArgumentException("پیوند جلسه باید یک نشانی HTTPS معتبر باشد.", nameof(meetingUrl));
        }
    }

    private static string? NormalizeMeetingUrl(string? meetingUrl) =>
        string.IsNullOrWhiteSpace(meetingUrl) ? null : meetingUrl.Trim();
}
