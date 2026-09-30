using MakanApp.Domain.Academic;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<Guid> CreateAcademicSessionAsync(
        Guid organizationId,
        Guid classId,
        DateTime startUtc,
        DateTime endUtc,
        SessionStatus status = SessionStatus.Scheduled,
        string? meetingUrl = null)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var session = Session.CreateScheduled(
            organizationId,
            classId,
            $"Session {Guid.NewGuid():N}",
            startUtc,
            endUtc,
            "UTC",
            meetingUrl,
            DateTime.UtcNow);
        if (status == SessionStatus.Cancelled)
        {
            session.Cancel(DateTime.UtcNow);
        }
        else if (status == SessionStatus.Completed)
        {
            session.Complete(DateTime.UtcNow);
        }

        dbContext.AcademicSessions.Add(session);
        await dbContext.SaveChangesAsync();
        return session.Id;
    }

    public async Task<int> CountSessionsAsync(Guid organizationId, Guid classId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.AcademicSessions.CountAsync(session =>
            session.OrganizationId == organizationId && session.ClassId == classId);
    }

    public async Task<SessionStatus> GetSessionStatusAsync(Guid sessionId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.AcademicSessions
            .Where(session => session.Id == sessionId)
            .Select(session => session.Status)
            .SingleAsync();
    }

    public async Task<int> CountAttendanceAsync(Guid organizationId, Guid sessionId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Attendance.CountAsync(attendance =>
            attendance.OrganizationId == organizationId &&
            attendance.SessionId == sessionId);
    }

    public async Task<int> CountAttendanceRevisionsAsync(Guid attendanceId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.AttendanceRevisions.CountAsync(
            revision => revision.AttendanceId == attendanceId);
    }

    public async Task<AttendanceStatus> GetAttendanceStatusAsync(Guid attendanceId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Attendance
            .Where(attendance => attendance.Id == attendanceId)
            .Select(attendance => attendance.Status)
            .SingleAsync();
    }

    public async Task<Guid> CreateAttendanceAsync(
        Guid organizationId,
        Guid classId,
        Guid sessionId,
        Guid enrollmentId,
        Guid recordedByMembershipId,
        AttendanceStatus status = AttendanceStatus.Present)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var attendance = Attendance.Record(
            organizationId,
            classId,
            sessionId,
            enrollmentId,
            status,
            DateTime.UtcNow,
            recordedByMembershipId);
        dbContext.Attendance.Add(attendance);
        await dbContext.SaveChangesAsync();
        return attendance.Id;
    }

    public async Task<bool> InvalidAttendanceReferenceIsRejectedAsync(
        Guid organizationId,
        Guid classId,
        Guid sessionId,
        Guid enrollmentId,
        Guid recordedByMembershipId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        dbContext.Attendance.Add(Attendance.Record(
            organizationId,
            classId,
            sessionId,
            enrollmentId,
            AttendanceStatus.Present,
            DateTime.UtcNow,
            recordedByMembershipId));
        try
        {
            await dbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }

    public async Task<bool> DuplicateAttendanceIsRejectedAsync(
        Guid organizationId,
        Guid classId,
        Guid sessionId,
        Guid enrollmentId,
        Guid recordedByMembershipId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        dbContext.Attendance.AddRange(
            Attendance.Record(
                organizationId,
                classId,
                sessionId,
                enrollmentId,
                AttendanceStatus.Present,
                nowUtc,
                recordedByMembershipId),
            Attendance.Record(
                organizationId,
                classId,
                sessionId,
                enrollmentId,
                AttendanceStatus.Late,
                nowUtc,
                recordedByMembershipId));
        try
        {
            await dbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }
}
