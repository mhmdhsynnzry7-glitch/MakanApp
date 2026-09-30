using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed partial class EfAcademicSessionStore
{
    public Task<Enrollment?> GetEnrollmentForAttendanceAsync(
        Guid organizationId,
        Guid classId,
        Guid enrollmentId,
        CancellationToken cancellationToken) =>
        _dbContext.Enrollments.SingleOrDefaultAsync(
            enrollment => enrollment.OrganizationId == organizationId &&
                          enrollment.ClassId == classId &&
                          enrollment.Id == enrollmentId,
            cancellationToken);

    public Task<Attendance?> GetAttendanceAsync(
        Guid organizationId,
        Guid sessionId,
        Guid enrollmentId,
        CancellationToken cancellationToken) =>
        _dbContext.Attendance.SingleOrDefaultAsync(
            attendance => attendance.OrganizationId == organizationId &&
                          attendance.SessionId == sessionId &&
                          attendance.EnrollmentId == enrollmentId,
            cancellationToken);

    public Task<Attendance?> GetAttendanceForUpdateAsync(
        Guid organizationId,
        Guid sessionId,
        Guid attendanceId,
        CancellationToken cancellationToken) =>
        _dbContext.Attendance
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[Attendance] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [SessionId] = {sessionId} AND [Id] = {attendanceId}")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AttendanceRosterRecord>> GetAttendanceRosterAsync(
        Guid organizationId,
        Guid classId,
        Guid sessionId,
        DateTime sessionStartUtc,
        CancellationToken cancellationToken) =>
        await (
            from enrollment in _dbContext.Enrollments
            join attendance in _dbContext.Attendance
                on new
                {
                    enrollment.OrganizationId,
                    enrollment.ClassId,
                    EnrollmentId = enrollment.Id,
                    SessionId = sessionId
                }
                equals new
                {
                    attendance.OrganizationId,
                    attendance.ClassId,
                    attendance.EnrollmentId,
                    attendance.SessionId
                }
                into attendanceGroup
            from attendance in attendanceGroup.DefaultIfEmpty()
            where enrollment.OrganizationId == organizationId &&
                  enrollment.ClassId == classId &&
                  enrollment.EnrolledAtUtc <= sessionStartUtc &&
                  (!enrollment.EndedAtUtc.HasValue || enrollment.EndedAtUtc >= sessionStartUtc)
            orderby enrollment.Id
            select new AttendanceRosterRecord(enrollment, attendance))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AttendanceRevision>> GetAttendanceRevisionsAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        await _dbContext.AttendanceRevisions
            .Where(revision =>
                revision.OrganizationId == organizationId &&
                revision.SessionId == sessionId)
            .OrderBy(revision => revision.CorrectedAtUtc)
            .ThenBy(revision => revision.Id)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
}
