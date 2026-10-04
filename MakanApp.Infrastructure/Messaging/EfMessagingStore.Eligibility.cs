using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Messaging;

public sealed partial class EfMessagingStore
{
    private async Task<CommunicationEligibilityFacts> LoadEligibilityFactsAsync(
        Guid actorUserId,
        Guid targetUserId,
        ConversationScope scope,
        Guid? organizationId,
        AccessContext accessContext,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var actor = await LoadUserSnapshotAsync(actorUserId, cancellationToken);
        var target = await LoadUserSnapshotAsync(targetUserId, cancellationToken);
        var pair = DirectUserPair.Create(actorUserId, targetUserId);
        var hasPersonalGrant = await dbContext.PersonalCommunicationGrants.AnyAsync(
            grant => grant.LowerUserId == pair.LowerUserId &&
                     grant.HigherUserId == pair.HigherUserId &&
                     grant.Status == PersonalCommunicationGrantStatus.Active &&
                     grant.EndedAtUtc == null,
            cancellationToken);

        var actorMembershipActive = false;
        var actorRoleActive = false;
        var targetMembershipActive = false;
        var targetRoles = new HashSet<OrganizationRole>();
        var actorHasEnrollment = false;
        var targetHasEnrollment = false;
        var hasTeacherStudentRelationship = false;
        var hasTeacherParentRelationship = false;

        if (scope == ConversationScope.Organization && organizationId.HasValue)
        {
            actorMembershipActive = accessContext.MembershipId.HasValue &&
                await dbContext.Memberships.AnyAsync(
                    membership => membership.Id == accessContext.MembershipId.Value &&
                                  membership.UserId == actorUserId &&
                                  membership.OrganizationId == organizationId.Value &&
                                  membership.Status == MembershipStatus.Active &&
                                  membership.EndedAtUtc == null,
                    cancellationToken);
            actorRoleActive = actorMembershipActive && accessContext.ActiveRole.HasValue &&
                await dbContext.RoleAssignments.AnyAsync(
                    role => role.MembershipId == accessContext.MembershipId!.Value &&
                            role.Role == accessContext.ActiveRole.Value &&
                            role.Status == RoleAssignmentStatus.Active &&
                            role.EndedAtUtc == null,
                    cancellationToken);

            var targetMembershipIds = await dbContext.Memberships
                .Where(membership => membership.UserId == targetUserId &&
                                     membership.OrganizationId == organizationId.Value &&
                                     membership.Status == MembershipStatus.Active &&
                                     membership.EndedAtUtc == null)
                .Select(membership => membership.Id)
                .ToArrayAsync(cancellationToken);
            targetMembershipActive = targetMembershipIds.Length > 0;
            if (targetMembershipActive)
            {
                targetRoles = (await dbContext.RoleAssignments
                    .Where(role => targetMembershipIds.Contains(role.MembershipId) &&
                                   role.Status == RoleAssignmentStatus.Active &&
                                   role.EndedAtUtc == null)
                    .Select(role => role.Role)
                    .Distinct()
                    .ToArrayAsync(cancellationToken))
                    .ToHashSet();
            }

            actorHasEnrollment = actor?.PersonId is not null &&
                await HasActiveEnrollmentAsync(
                    organizationId.Value,
                    actor.PersonId.Value,
                    cancellationToken);
            targetHasEnrollment = target?.PersonId is not null &&
                await HasActiveEnrollmentAsync(
                    organizationId.Value,
                    target.PersonId.Value,
                    cancellationToken);

            if (accessContext.MembershipId.HasValue && accessContext.ActiveRole.HasValue)
            {
                if (accessContext.ActiveRole == OrganizationRole.Teacher && target is not null)
                {
                    hasTeacherStudentRelationship = target.PersonId.HasValue &&
                        await HasTeacherStudentRelationshipAsync(
                            organizationId.Value,
                            accessContext.MembershipId.Value,
                            target.PersonId.Value,
                            cancellationToken);
                    hasTeacherParentRelationship =
                        await HasTeacherParentRelationshipAsync(
                            organizationId.Value,
                            accessContext.MembershipId.Value,
                            targetUserId,
                            nowUtc,
                            cancellationToken);
                }
                else if (accessContext.ActiveRole == OrganizationRole.Student && actor?.PersonId is not null)
                {
                    hasTeacherStudentRelationship =
                        await HasStudentTeacherRelationshipAsync(
                            organizationId.Value,
                            actor.PersonId.Value,
                            targetMembershipIds,
                            cancellationToken);
                }
                else if (accessContext.ActiveRole == OrganizationRole.Parent)
                {
                    hasTeacherParentRelationship =
                        await HasParentTeacherRelationshipAsync(
                            organizationId.Value,
                            actorUserId,
                            targetMembershipIds,
                            nowUtc,
                            cancellationToken);
                }
            }
        }

        return new CommunicationEligibilityFacts(
            actorUserId,
            targetUserId,
            scope,
            organizationId,
            accessContext,
            actor is not null,
            target is not null,
            actor?.AgeCategory ?? CommunicationAgeCategory.Unknown,
            target?.AgeCategory ?? CommunicationAgeCategory.Unknown,
            hasPersonalGrant,
            actorMembershipActive,
            actorRoleActive,
            targetMembershipActive,
            targetRoles,
            actorHasEnrollment,
            targetHasEnrollment,
            hasTeacherStudentRelationship,
            hasTeacherParentRelationship);
    }

    private async Task<SafeMessagingIdentityRecord> LoadSafeIdentityAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await LoadUserSnapshotAsync(userId, cancellationToken);
        if (user is null)
        {
            throw RecipientNotAvailable();
        }

        return new SafeMessagingIdentityRecord(user.UserId, user.Username, user.DisplayName);
    }

    private Task<UserMessagingSnapshot?> LoadUserSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        (from user in dbContext.Users
         join person in dbContext.Persons on user.PersonId equals person.Id
         where user.Id == userId &&
               user.PersonId != null &&
               user.Username != null &&
               user.ProfileCompletedAtUtc != null
         select new UserMessagingSnapshot(
             user.Id,
             user.PersonId,
             user.Username!,
             person.DisplayName ?? person.FirstName + " " + person.LastName,
             user.CommunicationAgeCategory))
        .AsNoTracking()
        .SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> HasActiveEnrollmentAsync(
        Guid organizationId,
        Guid personId,
        CancellationToken cancellationToken)
    {
        var organizationPersonIds = dbContext.OrganizationPersons
            .Where(person => person.OrganizationId == organizationId &&
                             person.PersonId == personId &&
                             person.Status == OrganizationPersonStatus.Active &&
                             person.EndedAtUtc == null)
            .Select(person => person.Id);
        return await (
            from enrollment in dbContext.Enrollments
            join academicClass in dbContext.Classes
                on new { enrollment.OrganizationId, enrollment.ClassId }
                equals new { academicClass.OrganizationId, ClassId = academicClass.Id }
            where enrollment.OrganizationId == organizationId &&
                  organizationPersonIds.Contains(enrollment.LearnerOrganizationPersonId) &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  academicClass.Status == ClassStatus.Active
            select enrollment.Id)
            .AnyAsync(cancellationToken);
    }

    private Task<bool> HasTeacherStudentRelationshipAsync(
        Guid organizationId,
        Guid teacherMembershipId,
        Guid studentPersonId,
        CancellationToken cancellationToken) =>
        (from organizationPerson in dbContext.OrganizationPersons
         join enrollment in dbContext.Enrollments
             on new { organizationPerson.OrganizationId, LearnerId = organizationPerson.Id }
             equals new { enrollment.OrganizationId, LearnerId = enrollment.LearnerOrganizationPersonId }
         join assignment in dbContext.TeacherAssignments
             on new { enrollment.OrganizationId, enrollment.ClassId }
             equals new { assignment.OrganizationId, assignment.ClassId }
         join academicClass in dbContext.Classes
             on new { enrollment.OrganizationId, enrollment.ClassId }
             equals new { academicClass.OrganizationId, ClassId = academicClass.Id }
         where organizationPerson.OrganizationId == organizationId &&
               organizationPerson.PersonId == studentPersonId &&
               organizationPerson.Status == OrganizationPersonStatus.Active &&
               organizationPerson.EndedAtUtc == null &&
               enrollment.Status == EnrollmentStatus.Active &&
               enrollment.EndedAtUtc == null &&
               assignment.TeacherMembershipId == teacherMembershipId &&
               assignment.Status == TeacherAssignmentStatus.Active &&
               assignment.EndedAtUtc == null &&
               academicClass.Status == ClassStatus.Active
         select enrollment.Id)
        .AnyAsync(cancellationToken);

    private Task<bool> HasStudentTeacherRelationshipAsync(
        Guid organizationId,
        Guid studentPersonId,
        IReadOnlyCollection<Guid> teacherMembershipIds,
        CancellationToken cancellationToken) =>
        teacherMembershipIds.Count == 0
            ? Task.FromResult(false)
            : (from organizationPerson in dbContext.OrganizationPersons
               join enrollment in dbContext.Enrollments
                   on new { organizationPerson.OrganizationId, LearnerId = organizationPerson.Id }
                   equals new { enrollment.OrganizationId, LearnerId = enrollment.LearnerOrganizationPersonId }
               join assignment in dbContext.TeacherAssignments
                   on new { enrollment.OrganizationId, enrollment.ClassId }
                   equals new { assignment.OrganizationId, assignment.ClassId }
               join academicClass in dbContext.Classes
                   on new { enrollment.OrganizationId, enrollment.ClassId }
                   equals new { academicClass.OrganizationId, ClassId = academicClass.Id }
               where organizationPerson.OrganizationId == organizationId &&
                     organizationPerson.PersonId == studentPersonId &&
                     organizationPerson.Status == OrganizationPersonStatus.Active &&
                     organizationPerson.EndedAtUtc == null &&
                     enrollment.Status == EnrollmentStatus.Active &&
                     enrollment.EndedAtUtc == null &&
                     teacherMembershipIds.Contains(assignment.TeacherMembershipId) &&
                     assignment.Status == TeacherAssignmentStatus.Active &&
                     assignment.EndedAtUtc == null &&
                     academicClass.Status == ClassStatus.Active
               select enrollment.Id)
            .AnyAsync(cancellationToken);

    private Task<bool> HasTeacherParentRelationshipAsync(
        Guid organizationId,
        Guid teacherMembershipId,
        Guid parentUserId,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        (from relation in dbContext.GuardianRelations
         join enrollment in dbContext.Enrollments
             on new { relation.OrganizationId, LearnerId = relation.LearnerOrganizationPersonId }
             equals new { enrollment.OrganizationId, LearnerId = enrollment.LearnerOrganizationPersonId }
         join assignment in dbContext.TeacherAssignments
             on new { enrollment.OrganizationId, enrollment.ClassId }
             equals new { assignment.OrganizationId, assignment.ClassId }
         join academicClass in dbContext.Classes
             on new { enrollment.OrganizationId, enrollment.ClassId }
             equals new { academicClass.OrganizationId, ClassId = academicClass.Id }
         where relation.OrganizationId == organizationId &&
               relation.GuardianUserId == parentUserId &&
               relation.Status == GuardianRelationStatus.Active &&
               relation.EndedAtUtc == null &&
               relation.ValidFromUtc <= nowUtc &&
               enrollment.Status == EnrollmentStatus.Active &&
               enrollment.EndedAtUtc == null &&
               assignment.TeacherMembershipId == teacherMembershipId &&
               assignment.Status == TeacherAssignmentStatus.Active &&
               assignment.EndedAtUtc == null &&
               academicClass.Status == ClassStatus.Active
         select relation.Id)
        .AnyAsync(cancellationToken);

    private Task<bool> HasParentTeacherRelationshipAsync(
        Guid organizationId,
        Guid parentUserId,
        IReadOnlyCollection<Guid> teacherMembershipIds,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        teacherMembershipIds.Count == 0
            ? Task.FromResult(false)
            : (from relation in dbContext.GuardianRelations
               join enrollment in dbContext.Enrollments
                   on new { relation.OrganizationId, LearnerId = relation.LearnerOrganizationPersonId }
                   equals new { enrollment.OrganizationId, LearnerId = enrollment.LearnerOrganizationPersonId }
               join assignment in dbContext.TeacherAssignments
                   on new { enrollment.OrganizationId, enrollment.ClassId }
                   equals new { assignment.OrganizationId, assignment.ClassId }
               join academicClass in dbContext.Classes
                   on new { enrollment.OrganizationId, enrollment.ClassId }
                   equals new { academicClass.OrganizationId, ClassId = academicClass.Id }
               where relation.OrganizationId == organizationId &&
                     relation.GuardianUserId == parentUserId &&
                     relation.Status == GuardianRelationStatus.Active &&
                     relation.EndedAtUtc == null &&
                     relation.ValidFromUtc <= nowUtc &&
                     enrollment.Status == EnrollmentStatus.Active &&
                     enrollment.EndedAtUtc == null &&
                     teacherMembershipIds.Contains(assignment.TeacherMembershipId) &&
                     assignment.Status == TeacherAssignmentStatus.Active &&
                     assignment.EndedAtUtc == null &&
                     academicClass.Status == ClassStatus.Active
               select relation.Id)
            .AnyAsync(cancellationToken);

    private sealed record UserMessagingSnapshot(
        Guid UserId,
        Guid? PersonId,
        string Username,
        string DisplayName,
        CommunicationAgeCategory AgeCategory);
}
