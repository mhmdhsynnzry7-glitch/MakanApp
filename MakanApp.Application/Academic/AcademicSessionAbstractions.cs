using MakanApp.Domain.Academic;

namespace MakanApp.Application.Academic;

public interface IAcademicSessionService
{
    Task<SessionResult> CreateSessionAsync(
        Guid userId,
        Guid userSessionId,
        Guid classId,
        CreateSessionCommand command,
        CancellationToken cancellationToken);

    Task<SessionResult> UpdateSessionAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        UpdateSessionCommand command,
        CancellationToken cancellationToken);

    Task<SessionResult> CancelSessionAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        SessionVersionCommand command,
        CancellationToken cancellationToken);

    Task<SessionResult> CompleteSessionAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        SessionVersionCommand command,
        CancellationToken cancellationToken);

    Task<SessionResult> GetSessionAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SessionResult>> GetScheduleAsync(
        Guid userId,
        Guid userSessionId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    Task<ScheduleRuleResult> CreateScheduleRuleAsync(
        Guid userId,
        Guid userSessionId,
        Guid classId,
        CreateScheduleRuleCommand command,
        CancellationToken cancellationToken);

    Task<ScheduleRuleResult> UpdateScheduleRuleAsync(
        Guid userId,
        Guid userSessionId,
        Guid scheduleRuleId,
        UpdateScheduleRuleCommand command,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AttendanceEntryResult>> RecordAttendanceAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        RecordAttendanceCommand command,
        CancellationToken cancellationToken);

    Task<AttendanceEntryResult> CorrectAttendanceAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        Guid attendanceId,
        CorrectAttendanceCommand command,
        CancellationToken cancellationToken);

    Task<SessionAttendanceResult> GetSessionAttendanceAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        CancellationToken cancellationToken);
}

public interface IAcademicSessionStore
{
    Task<IAcademicTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken);

    Task<Class?> GetClassForUpdateAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken);

    Task<SessionWithClassRecord?> GetSessionAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<SessionWithClassRecord?> GetSessionForUpdateAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<ScheduleRule?> GetScheduleRuleForUpdateAsync(
        Guid organizationId,
        Guid scheduleRuleId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Session>> GetFutureRuleSessionsAsync(
        Guid organizationId,
        Guid scheduleRuleId,
        DateOnly applyFromDate,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<bool> HasActiveTeacherAssignmentAsync(
        Guid organizationId,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken);

    Task<bool> HasClassTimeConflictAsync(
        Guid organizationId,
        Guid classId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? excludedSessionId,
        CancellationToken cancellationToken);

    Task<bool> HasTeacherTimeConflictAsync(
        Guid organizationId,
        Guid classId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? excludedSessionId,
        CancellationToken cancellationToken);

    Task<bool> CanStudentAccessClassAsync(
        Guid organizationId,
        Guid classId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> CanLearnerAccessClassAsync(
        Guid organizationId,
        Guid classId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SessionWithClassRecord>> GetScheduleForManagerAsync(
        Guid organizationId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SessionWithClassRecord>> GetScheduleForTeacherAsync(
        Guid organizationId,
        Guid membershipId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SessionWithClassRecord>> GetScheduleForStudentAsync(
        Guid organizationId,
        Guid userId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SessionWithClassRecord>> GetScheduleForLearnerAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    Task<Enrollment?> GetEnrollmentForAttendanceAsync(
        Guid organizationId,
        Guid classId,
        Guid enrollmentId,
        CancellationToken cancellationToken);

    Task<Attendance?> GetAttendanceAsync(
        Guid organizationId,
        Guid sessionId,
        Guid enrollmentId,
        CancellationToken cancellationToken);

    Task<Attendance?> GetAttendanceForUpdateAsync(
        Guid organizationId,
        Guid sessionId,
        Guid attendanceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AttendanceRosterRecord>> GetAttendanceRosterAsync(
        Guid organizationId,
        Guid classId,
        Guid sessionId,
        DateTime sessionStartUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AttendanceRevision>> GetAttendanceRevisionsAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken);

    void SetOriginalRowVersion(Session session, byte[] rowVersion);
    void SetOriginalRowVersion(ScheduleRule scheduleRule, byte[] rowVersion);
    void SetOriginalRowVersion(Attendance attendance, byte[] rowVersion);
    void Add(Session session);
    void Add(ScheduleRule scheduleRule);
    void Add(Attendance attendance);
    void Add(AttendanceRevision revision);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
