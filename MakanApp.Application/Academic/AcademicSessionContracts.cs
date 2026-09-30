using MakanApp.Domain.Academic;

namespace MakanApp.Application.Academic;

public sealed record CreateSessionCommand(
    string Title,
    DateTime StartUtc,
    DateTime EndUtc,
    string TimeZoneId,
    string? MeetingUrl);

public sealed record UpdateSessionCommand(
    string Title,
    DateTime StartUtc,
    DateTime EndUtc,
    string TimeZoneId,
    string? MeetingUrl,
    string ExpectedRowVersion);

public sealed record SessionVersionCommand(string ExpectedRowVersion);

public sealed record SessionResult(
    Guid Id,
    Guid OrganizationId,
    Guid ClassId,
    string ClassTitle,
    Guid? ScheduleRuleId,
    string Title,
    DateTime StartUtc,
    DateTime EndUtc,
    string TimeZoneId,
    string? MeetingUrl,
    SessionStatus Status,
    DateTime? CancelledAtUtc,
    DateTime? CompletedAtUtc,
    string RowVersion);

public sealed record CreateScheduleRuleCommand(
    DayOfWeek LocalDayOfWeek,
    TimeOnly LocalStartTime,
    int DurationMinutes,
    string TimeZoneId,
    DateOnly EffectiveFrom,
    DateOnly EffectiveUntil,
    string SessionTitle,
    string? MeetingUrl);

public sealed record UpdateScheduleRuleCommand(
    DayOfWeek LocalDayOfWeek,
    TimeOnly LocalStartTime,
    int DurationMinutes,
    string TimeZoneId,
    DateOnly EffectiveFrom,
    DateOnly EffectiveUntil,
    DateOnly ApplyFromDate,
    string SessionTitle,
    string? MeetingUrl,
    string ExpectedRowVersion);

public sealed record ScheduleRuleResult(
    Guid Id,
    Guid OrganizationId,
    Guid ClassId,
    DayOfWeek LocalDayOfWeek,
    TimeOnly LocalStartTime,
    int DurationMinutes,
    string TimeZoneId,
    DateOnly EffectiveFrom,
    DateOnly EffectiveUntil,
    string SessionTitle,
    string? MeetingUrl,
    ScheduleRuleStatus Status,
    int GeneratedSessionCount,
    string RowVersion);

public sealed record RecordAttendanceEntryCommand(
    Guid EnrollmentId,
    AttendanceStatus Status);

public sealed record RecordAttendanceCommand(
    IReadOnlyCollection<RecordAttendanceEntryCommand> Entries);

public sealed record CorrectAttendanceCommand(
    AttendanceStatus Status,
    string CorrectionReason,
    string ExpectedRowVersion);

public sealed record AttendanceEntryResult(
    Guid? AttendanceId,
    Guid EnrollmentId,
    Guid LearnerOrganizationPersonId,
    AttendanceStatus Status,
    DateTime? RecordedAtUtc,
    Guid? RecordedByMembershipId,
    string? RowVersion);

public sealed record AttendanceRevisionResult(
    Guid Id,
    AttendanceStatus PreviousStatus,
    AttendanceStatus NewStatus,
    DateTime CorrectedAtUtc,
    Guid CorrectedByMembershipId,
    string Reason);

public sealed record SessionAttendanceResult(
    Guid SessionId,
    SessionStatus SessionStatus,
    IReadOnlyCollection<AttendanceEntryResult> Entries,
    IReadOnlyCollection<AttendanceRevisionResult> Revisions);

public sealed record SessionWithClassRecord(Session Session, string ClassTitle);

public sealed record AttendanceRosterRecord(
    Enrollment Enrollment,
    Attendance? Attendance);
