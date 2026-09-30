using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Academic;

public sealed partial class EfAcademicSessionStore
{
    public Task<bool> CanStudentAccessClassAsync(
        Guid organizationId,
        Guid classId,
        Guid userId,
        CancellationToken cancellationToken) =>
        (
            from enrollment in _dbContext.Enrollments
            join organizationPerson in _dbContext.OrganizationPersons
                on new
                {
                    enrollment.OrganizationId,
                    Id = enrollment.LearnerOrganizationPersonId
                }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join user in _dbContext.Users
                on organizationPerson.PersonId equals user.PersonId
            where user.Id == userId &&
                  enrollment.OrganizationId == organizationId &&
                  enrollment.ClassId == classId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            select enrollment.Id)
            .AnyAsync(cancellationToken);

    public Task<bool> CanLearnerAccessClassAsync(
        Guid organizationId,
        Guid classId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken) =>
        _dbContext.Enrollments.AnyAsync(
            enrollment => enrollment.OrganizationId == organizationId &&
                          enrollment.ClassId == classId &&
                          enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                          enrollment.Status == EnrollmentStatus.Active &&
                          enrollment.EndedAtUtc == null,
            cancellationToken);

    public async Task<IReadOnlyCollection<SessionWithClassRecord>> GetScheduleForManagerAsync(
        Guid organizationId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken) =>
        await CreateScheduleQuery(organizationId, fromUtc, toUtc)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<SessionWithClassRecord>> GetScheduleForTeacherAsync(
        Guid organizationId,
        Guid membershipId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken) =>
        await (
            from session in _dbContext.AcademicSessions
            join academicClass in _dbContext.Classes
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join assignment in _dbContext.TeacherAssignments
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { assignment.OrganizationId, Id = assignment.ClassId }
            where session.OrganizationId == organizationId &&
                  session.StartUtc < toUtc &&
                  session.EndUtc > fromUtc &&
                  assignment.TeacherMembershipId == membershipId &&
                  assignment.Status == TeacherAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null
            orderby session.StartUtc, session.Id
            select new SessionWithClassRecord(session, academicClass.Title))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<SessionWithClassRecord>> GetScheduleForStudentAsync(
        Guid organizationId,
        Guid userId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken) =>
        await (
            from session in _dbContext.AcademicSessions
            join academicClass in _dbContext.Classes
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join enrollment in _dbContext.Enrollments
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { enrollment.OrganizationId, Id = enrollment.ClassId }
            join organizationPerson in _dbContext.OrganizationPersons
                on new
                {
                    enrollment.OrganizationId,
                    Id = enrollment.LearnerOrganizationPersonId
                }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join user in _dbContext.Users
                on organizationPerson.PersonId equals user.PersonId
            where session.OrganizationId == organizationId &&
                  session.StartUtc < toUtc &&
                  session.EndUtc > fromUtc &&
                  user.Id == userId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            orderby session.StartUtc, session.Id
            select new SessionWithClassRecord(session, academicClass.Title))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<SessionWithClassRecord>> GetScheduleForLearnerAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken) =>
        await (
            from session in _dbContext.AcademicSessions
            join academicClass in _dbContext.Classes
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join enrollment in _dbContext.Enrollments
                on new { session.OrganizationId, Id = session.ClassId }
                equals new { enrollment.OrganizationId, Id = enrollment.ClassId }
            where session.OrganizationId == organizationId &&
                  session.StartUtc < toUtc &&
                  session.EndUtc > fromUtc &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null
            orderby session.StartUtc, session.Id
            select new SessionWithClassRecord(session, academicClass.Title))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    private IQueryable<SessionWithClassRecord> CreateScheduleQuery(
        Guid organizationId,
        DateTime fromUtc,
        DateTime toUtc) =>
        from session in _dbContext.AcademicSessions
        join academicClass in _dbContext.Classes
            on new { session.OrganizationId, Id = session.ClassId }
            equals new { academicClass.OrganizationId, academicClass.Id }
        where session.OrganizationId == organizationId &&
              session.StartUtc < toUtc &&
              session.EndUtc > fromUtc
        orderby session.StartUtc, session.Id
        select new SessionWithClassRecord(session, academicClass.Title);
}
