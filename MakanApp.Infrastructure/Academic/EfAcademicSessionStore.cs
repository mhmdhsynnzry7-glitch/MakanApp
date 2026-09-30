using System.Data;
using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Academic;

public sealed partial class EfAcademicSessionStore : IAcademicSessionStore
{
    private readonly MakanDbContext _dbContext;

    public EfAcademicSessionStore(MakanDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IAcademicTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfSessionTransaction(transaction);
    }

    public Task<AcademicClass?> GetClassForUpdateAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken) =>
        _dbContext.Classes
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[Classes] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {classId}")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SessionWithClassRecord?> GetSessionAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        await (
            from session in _dbContext.AcademicSessions
            join academicClass in _dbContext.Classes
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            where session.OrganizationId == organizationId && session.Id == sessionId
            select new SessionWithClassRecord(session, academicClass.Title))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<SessionWithClassRecord?> GetSessionForUpdateAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _dbContext.AcademicSessions
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[Sessions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {sessionId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            return null;
        }

        var classTitle = await _dbContext.Classes
            .Where(academicClass =>
                academicClass.OrganizationId == organizationId &&
                academicClass.Id == session.ClassId)
            .Select(academicClass => academicClass.Title)
            .SingleAsync(cancellationToken);
        return new SessionWithClassRecord(session, classTitle);
    }

    public Task<ScheduleRule?> GetScheduleRuleForUpdateAsync(
        Guid organizationId,
        Guid scheduleRuleId,
        CancellationToken cancellationToken) =>
        _dbContext.ScheduleRules
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[ScheduleRules] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {scheduleRuleId}")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Session>> GetFutureRuleSessionsAsync(
        Guid organizationId,
        Guid scheduleRuleId,
        DateOnly applyFromDate,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        await _dbContext.AcademicSessions
            .Where(session =>
                session.OrganizationId == organizationId &&
                session.ScheduleRuleId == scheduleRuleId &&
                session.OccurrenceLocalDate >= applyFromDate &&
                session.StartUtc > nowUtc &&
                session.Status == SessionStatus.Scheduled)
            .ToArrayAsync(cancellationToken);

    public Task<bool> HasActiveTeacherAssignmentAsync(
        Guid organizationId,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        (
            from assignment in _dbContext.TeacherAssignments
            join membership in _dbContext.Memberships
                on new { assignment.OrganizationId, Id = assignment.TeacherMembershipId }
                equals new { membership.OrganizationId, membership.Id }
            join role in _dbContext.RoleAssignments
                on membership.Id equals role.MembershipId
            where assignment.OrganizationId == organizationId &&
                  assignment.ClassId == classId &&
                  assignment.TeacherMembershipId == membershipId &&
                  assignment.Status == TeacherAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null &&
                  membership.Status == MembershipStatus.Active &&
                  membership.EndedAtUtc == null &&
                  role.Role == OrganizationRole.Teacher &&
                  role.Status == RoleAssignmentStatus.Active &&
                  role.EndedAtUtc == null
            select assignment.Id)
            .AnyAsync(cancellationToken);

    public Task<bool> HasClassTimeConflictAsync(
        Guid organizationId,
        Guid classId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? excludedSessionId,
        CancellationToken cancellationToken) =>
        _dbContext.AcademicSessions.AnyAsync(
            session => session.OrganizationId == organizationId &&
                       session.ClassId == classId &&
                       session.Status != SessionStatus.Cancelled &&
                       (!excludedSessionId.HasValue || session.Id != excludedSessionId.Value) &&
                       session.StartUtc < endUtc &&
                       session.EndUtc > startUtc,
            cancellationToken);

    public Task<bool> HasTeacherTimeConflictAsync(
        Guid organizationId,
        Guid classId,
        DateTime startUtc,
        DateTime endUtc,
        Guid? excludedSessionId,
        CancellationToken cancellationToken)
    {
        var targetTeacherUserIds =
            from assignment in _dbContext.TeacherAssignments
            join membership in _dbContext.Memberships
                on new { assignment.OrganizationId, Id = assignment.TeacherMembershipId }
                equals new { membership.OrganizationId, membership.Id }
            where assignment.OrganizationId == organizationId &&
                  assignment.ClassId == classId &&
                  assignment.Status == TeacherAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null &&
                  membership.Status == MembershipStatus.Active &&
                  membership.EndedAtUtc == null
            select membership.UserId;

        return (
            from session in _dbContext.AcademicSessions
            join assignment in _dbContext.TeacherAssignments
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { assignment.OrganizationId, Id = assignment.ClassId }
            join membership in _dbContext.Memberships
                on new { assignment.OrganizationId, Id = assignment.TeacherMembershipId }
                equals new { membership.OrganizationId, membership.Id }
            where targetTeacherUserIds.Contains(membership.UserId) &&
                  assignment.Status == TeacherAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null &&
                  membership.Status == MembershipStatus.Active &&
                  membership.EndedAtUtc == null &&
                  session.Status != SessionStatus.Cancelled &&
                  (!excludedSessionId.HasValue || session.Id != excludedSessionId.Value) &&
                  session.StartUtc < endUtc &&
                  session.EndUtc > startUtc
            select session.Id)
            .AnyAsync(cancellationToken);
    }

    public void SetOriginalRowVersion(Session session, byte[] rowVersion) =>
        _dbContext.Entry(session).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public void SetOriginalRowVersion(ScheduleRule scheduleRule, byte[] rowVersion) =>
        _dbContext.Entry(scheduleRule).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public void SetOriginalRowVersion(Attendance attendance, byte[] rowVersion) =>
        _dbContext.Entry(attendance).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public void Add(Session session) => _dbContext.AcademicSessions.Add(session);
    public void Add(ScheduleRule scheduleRule) => _dbContext.ScheduleRules.Add(scheduleRule);
    public void Add(Attendance attendance) => _dbContext.Attendance.Add(attendance);
    public void Add(AttendanceRevision revision) => _dbContext.AttendanceRevisions.Add(revision);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AcademicException(
                AcademicErrorCodes.ConcurrencyConflict,
                "اطلاعات هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  sqlException.Message.Contains(
                      "UX_Attendance_Organization_Session_Enrollment",
                      StringComparison.Ordinal))
        {
            throw new AcademicException(
                AcademicErrorCodes.AttendanceAlreadyRecorded,
                "حضور این ثبت‌نام قبلاً ثبت شده است.");
        }
    }

    private sealed class EfSessionTransaction(IDbContextTransaction transaction)
        : IAcademicTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
