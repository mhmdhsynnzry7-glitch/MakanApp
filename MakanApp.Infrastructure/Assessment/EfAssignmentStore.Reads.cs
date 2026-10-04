using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Assessment;

public sealed partial class EfAssignmentStore
{
    public Task<AssignmentWithVersionRecord?> GetAssignmentAsync(
        Guid organizationId,
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        (
            from assignment in _dbContext.Assignments
            join version in _dbContext.AssignmentVersions
                on new
                {
                    assignment.OrganizationId,
                    AssignmentId = assignment.Id,
                    VersionNumber = assignment.CurrentVersionNumber
                }
                equals new
                {
                    version.OrganizationId,
                    version.AssignmentId,
                    version.VersionNumber
                }
            join academicClass in _dbContext.Classes
                on new { assignment.OrganizationId, Id = assignment.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            where assignment.OrganizationId == organizationId &&
                  assignment.Id == assignmentId
            select new AssignmentWithVersionRecord(
                assignment,
                version,
                academicClass.Title,
                academicClass.Status == ClassStatus.Active && academicClass.EndedAtUtc == null))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> IsRecipientForStudentAsync(
        Guid organizationId,
        Guid assignmentVersionId,
        Guid userId,
        CancellationToken cancellationToken) =>
        (
            from recipient in _dbContext.AssignmentRecipients
            join enrollment in _dbContext.Enrollments
                on new
                {
                    recipient.OrganizationId,
                    recipient.ClassId,
                    Id = recipient.EnrollmentId
                }
                equals new { enrollment.OrganizationId, enrollment.ClassId, enrollment.Id }
            join organizationPerson in _dbContext.OrganizationPersons
                on new
                {
                    enrollment.OrganizationId,
                    Id = enrollment.LearnerOrganizationPersonId
                }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join user in _dbContext.Users
                on organizationPerson.PersonId equals user.PersonId
            where recipient.OrganizationId == organizationId &&
                  recipient.AssignmentVersionId == assignmentVersionId &&
                  user.Id == userId &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            select recipient.Id)
            .AnyAsync(cancellationToken);

    public Task<bool> IsRecipientForLearnerAsync(
        Guid organizationId,
        Guid assignmentVersionId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken) =>
        (
            from recipient in _dbContext.AssignmentRecipients
            join enrollment in _dbContext.Enrollments
                on new
                {
                    recipient.OrganizationId,
                    recipient.ClassId,
                    Id = recipient.EnrollmentId
                }
                equals new { enrollment.OrganizationId, enrollment.ClassId, enrollment.Id }
            where recipient.OrganizationId == organizationId &&
                  recipient.AssignmentVersionId == assignmentVersionId &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId
            select recipient.Id)
            .AnyAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForManagerAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken) =>
        await CreateClassQuery(organizationId, classId)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForTeacherAsync(
        Guid organizationId,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        if (!await HasActiveTeacherAssignmentAsync(
                organizationId,
                classId,
                membershipId,
                cancellationToken))
        {
            return [];
        }

        return await CreateClassQuery(organizationId, classId)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForStudentAsync(
        Guid organizationId,
        Guid classId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await (
            from assignment in _dbContext.Assignments
            join version in _dbContext.AssignmentVersions
                on new
                {
                    assignment.OrganizationId,
                    AssignmentId = assignment.Id,
                    VersionNumber = assignment.CurrentVersionNumber
                }
                equals new
                {
                    version.OrganizationId,
                    version.AssignmentId,
                    version.VersionNumber
                }
            join academicClass in _dbContext.Classes
                on new { assignment.OrganizationId, Id = assignment.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join recipient in _dbContext.AssignmentRecipients
                on new
                {
                    version.OrganizationId,
                    AssignmentVersionId = version.Id
                }
                equals new
                {
                    recipient.OrganizationId,
                    recipient.AssignmentVersionId
                }
            join enrollment in _dbContext.Enrollments
                on new
                {
                    recipient.OrganizationId,
                    recipient.ClassId,
                    Id = recipient.EnrollmentId
                }
                equals new { enrollment.OrganizationId, enrollment.ClassId, enrollment.Id }
            join organizationPerson in _dbContext.OrganizationPersons
                on new
                {
                    enrollment.OrganizationId,
                    Id = enrollment.LearnerOrganizationPersonId
                }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join user in _dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where assignment.OrganizationId == organizationId &&
                  assignment.ClassId == classId &&
                  (assignment.Status == AssignmentStatus.Published ||
                   assignment.Status == AssignmentStatus.Closed) &&
                  user.Id == userId &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null
            orderby version.DueAtUtc, assignment.Id
            select new AssignmentWithVersionRecord(
                assignment,
                version,
                academicClass.Title,
                academicClass.Status == ClassStatus.Active && academicClass.EndedAtUtc == null))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForLearnerAsync(
        Guid organizationId,
        Guid classId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken) =>
        await (
            from assignment in _dbContext.Assignments
            join version in _dbContext.AssignmentVersions
                on new
                {
                    assignment.OrganizationId,
                    AssignmentId = assignment.Id,
                    VersionNumber = assignment.CurrentVersionNumber
                }
                equals new
                {
                    version.OrganizationId,
                    version.AssignmentId,
                    version.VersionNumber
                }
            join academicClass in _dbContext.Classes
                on new { assignment.OrganizationId, Id = assignment.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join recipient in _dbContext.AssignmentRecipients
                on new
                {
                    version.OrganizationId,
                    AssignmentVersionId = version.Id
                }
                equals new
                {
                    recipient.OrganizationId,
                    recipient.AssignmentVersionId
                }
            join enrollment in _dbContext.Enrollments
                on new
                {
                    recipient.OrganizationId,
                    recipient.ClassId,
                    Id = recipient.EnrollmentId
                }
                equals new { enrollment.OrganizationId, enrollment.ClassId, enrollment.Id }
            where assignment.OrganizationId == organizationId &&
                  assignment.ClassId == classId &&
                  (assignment.Status == AssignmentStatus.Published ||
                   assignment.Status == AssignmentStatus.Closed) &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId
            orderby version.DueAtUtc, assignment.Id
            select new AssignmentWithVersionRecord(
                assignment,
                version,
                academicClass.Title,
                academicClass.Status == ClassStatus.Active && academicClass.EndedAtUtc == null))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    private IQueryable<AssignmentWithVersionRecord> CreateClassQuery(
        Guid organizationId,
        Guid classId) =>
        from assignment in _dbContext.Assignments
        join version in _dbContext.AssignmentVersions
            on new
            {
                assignment.OrganizationId,
                AssignmentId = assignment.Id,
                VersionNumber = assignment.CurrentVersionNumber
            }
            equals new
            {
                version.OrganizationId,
                version.AssignmentId,
                version.VersionNumber
            }
        join academicClass in _dbContext.Classes
            on new { assignment.OrganizationId, Id = assignment.ClassId }
            equals new { academicClass.OrganizationId, academicClass.Id }
        where assignment.OrganizationId == organizationId && assignment.ClassId == classId
        orderby assignment.UpdatedAtUtc descending, assignment.Id
        select new AssignmentWithVersionRecord(
            assignment,
            version,
            academicClass.Title,
            academicClass.Status == ClassStatus.Active && academicClass.EndedAtUtc == null);
}
