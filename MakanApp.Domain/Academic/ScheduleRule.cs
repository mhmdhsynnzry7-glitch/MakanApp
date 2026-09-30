namespace MakanApp.Domain.Academic;

public sealed class ScheduleRule
{
    private ScheduleRule()
    {
    }

    private ScheduleRule(
        Guid id,
        Guid organizationId,
        Guid classId,
        DayOfWeek localDayOfWeek,
        TimeOnly localStartTime,
        int durationMinutes,
        string timeZoneId,
        DateOnly effectiveFrom,
        DateOnly effectiveUntil,
        string sessionTitle,
        string? meetingUrl,
        DateTime createdAtUtc)
    {
        Id = id;
        OrganizationId = organizationId;
        ClassId = classId;
        LocalDayOfWeek = localDayOfWeek;
        LocalStartTime = localStartTime;
        DurationMinutes = durationMinutes;
        TimeZoneId = timeZoneId;
        EffectiveFrom = effectiveFrom;
        EffectiveUntil = effectiveUntil;
        SessionTitle = sessionTitle;
        MeetingUrl = meetingUrl;
        Status = ScheduleRuleStatus.Active;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public DayOfWeek LocalDayOfWeek { get; private set; }
    public TimeOnly LocalStartTime { get; private set; }
    public int DurationMinutes { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly EffectiveUntil { get; private set; }
    public string SessionTitle { get; private set; } = string.Empty;
    public string? MeetingUrl { get; private set; }
    public ScheduleRuleStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == ScheduleRuleStatus.Active && !EndedAtUtc.HasValue;

    public static ScheduleRule Create(
        Guid organizationId,
        Guid classId,
        DayOfWeek localDayOfWeek,
        TimeOnly localStartTime,
        int durationMinutes,
        string timeZoneId,
        DateOnly effectiveFrom,
        DateOnly effectiveUntil,
        string sessionTitle,
        string? meetingUrl,
        DateTime createdAtUtc)
    {
        Validate(
            localDayOfWeek,
            durationMinutes,
            timeZoneId,
            effectiveFrom,
            effectiveUntil,
            sessionTitle,
            meetingUrl);
        return new ScheduleRule(
            Guid.NewGuid(),
            organizationId,
            classId,
            localDayOfWeek,
            localStartTime,
            durationMinutes,
            timeZoneId.Trim(),
            effectiveFrom,
            effectiveUntil,
            sessionTitle.Trim(),
            NormalizeMeetingUrl(meetingUrl),
            createdAtUtc);
    }

    public void UpdateForFuture(
        DayOfWeek localDayOfWeek,
        TimeOnly localStartTime,
        int durationMinutes,
        string timeZoneId,
        DateOnly effectiveFrom,
        DateOnly effectiveUntil,
        string sessionTitle,
        string? meetingUrl)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("برنامه تکرارشونده فعال نیست.");
        }

        Validate(
            localDayOfWeek,
            durationMinutes,
            timeZoneId,
            effectiveFrom,
            effectiveUntil,
            sessionTitle,
            meetingUrl);
        LocalDayOfWeek = localDayOfWeek;
        LocalStartTime = localStartTime;
        DurationMinutes = durationMinutes;
        TimeZoneId = timeZoneId.Trim();
        EffectiveFrom = effectiveFrom;
        EffectiveUntil = effectiveUntil;
        SessionTitle = sessionTitle.Trim();
        MeetingUrl = NormalizeMeetingUrl(meetingUrl);
    }

    public IEnumerable<DateOnly> GetOccurrenceDates(DateOnly fromDate)
    {
        var date = fromDate > EffectiveFrom ? fromDate : EffectiveFrom;
        while (date <= EffectiveUntil)
        {
            if (date.DayOfWeek == LocalDayOfWeek)
            {
                yield return date;
            }

            date = date.AddDays(1);
        }
    }

    public (DateTime StartUtc, DateTime EndUtc) GetUtcRange(DateOnly localDate)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var localStart = localDate.ToDateTime(LocalStartTime, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(localStart) || timeZone.IsAmbiguousTime(localStart))
        {
            throw new InvalidOperationException("زمان محلی این رخداد به دلیل تغییر ساعت معتبر و یکتا نیست.");
        }

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
        return (startUtc, startUtc.AddMinutes(DurationMinutes));
    }

    private static void Validate(
        DayOfWeek localDayOfWeek,
        int durationMinutes,
        string timeZoneId,
        DateOnly effectiveFrom,
        DateOnly effectiveUntil,
        string sessionTitle,
        string? meetingUrl)
    {
        if (!Enum.IsDefined(localDayOfWeek))
        {
            throw new ArgumentOutOfRangeException(
                nameof(localDayOfWeek),
                "روز هفته معتبر نیست.");
        }

        if (durationMinutes is <= 0 or > 1440)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationMinutes),
                "مدت جلسه باید بین یک دقیقه و یک روز باشد.");
        }

        if (effectiveUntil < effectiveFrom || effectiveUntil > effectiveFrom.AddYears(1))
        {
            throw new ArgumentException("بازه برنامه تکرارشونده معتبر نیست.");
        }

        _ = Session.CreateScheduled(
            Guid.NewGuid(),
            Guid.NewGuid(),
            sessionTitle,
            DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Utc),
            DateTime.SpecifyKind(DateTime.UnixEpoch.AddMinutes(durationMinutes), DateTimeKind.Utc),
            timeZoneId,
            meetingUrl,
            DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Utc));
    }

    private static string? NormalizeMeetingUrl(string? meetingUrl) =>
        string.IsNullOrWhiteSpace(meetingUrl) ? null : meetingUrl.Trim();
}
