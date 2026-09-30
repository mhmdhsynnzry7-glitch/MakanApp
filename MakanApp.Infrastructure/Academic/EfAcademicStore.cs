using System.Data;
using MakanApp.Application.Academic;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Academic;

public sealed class EfAcademicStore(MakanDbContext dbContext) : IAcademicStore
{
    public async Task<IAcademicTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfAcademicTransaction(transaction);
    }

    public Task<AcademicPeriod?> GetAcademicPeriodAsync(
        Guid organizationId,
        Guid academicPeriodId,
        CancellationToken cancellationToken) =>
        dbContext.AcademicPeriods.SingleOrDefaultAsync(
            period => period.OrganizationId == organizationId && period.Id == academicPeriodId,
            cancellationToken);

    public Task<Course?> GetCourseAsync(
        Guid organizationId,
        Guid courseId,
        CancellationToken cancellationToken) =>
        dbContext.Courses.SingleOrDefaultAsync(
            course => course.OrganizationId == organizationId && course.Id == courseId,
            cancellationToken);

    public Task<AcademicClass?> GetClassForUpdateAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken) =>
        dbContext.Classes
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[Classes] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {classId}")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<OrganizationPerson?> GetActiveOrganizationPersonAsync(
        Guid organizationId,
        Guid organizationPersonId,
        CancellationToken cancellationToken) =>
        dbContext.OrganizationPersons.SingleOrDefaultAsync(
            person => person.OrganizationId == organizationId &&
                      person.Id == organizationPersonId &&
                      person.Status == OrganizationPersonStatus.Active &&
                      person.EndedAtUtc == null,
            cancellationToken);

    public Task<Enrollment?> GetActiveEnrollmentAsync(
        Guid organizationId,
        Guid classId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken) =>
        dbContext.Enrollments.SingleOrDefaultAsync(
            enrollment => enrollment.OrganizationId == organizationId &&
                          enrollment.ClassId == classId &&
                          enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                          enrollment.Status == EnrollmentStatus.Active &&
                          enrollment.EndedAtUtc == null,
            cancellationToken);

    public Task<Enrollment?> GetEnrollmentForUpdateAsync(
        Guid organizationId,
        Guid classId,
        Guid enrollmentId,
        CancellationToken cancellationToken) =>
        dbContext.Enrollments
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[Enrollments] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ClassId] = {classId} AND [Id] = {enrollmentId}")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountActiveEnrollmentsAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken) =>
        dbContext.Enrollments.CountAsync(
            enrollment => enrollment.OrganizationId == organizationId &&
                          enrollment.ClassId == classId &&
                          enrollment.Status == EnrollmentStatus.Active &&
                          enrollment.EndedAtUtc == null,
            cancellationToken);

    public Task<bool> HasActiveTeacherAuthorityAsync(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        (
            from membership in dbContext.Memberships
            join roleAssignment in dbContext.RoleAssignments
                on membership.Id equals roleAssignment.MembershipId
            where membership.Id == membershipId &&
                  membership.OrganizationId == organizationId &&
                  membership.Status == MembershipStatus.Active &&
                  membership.EndedAtUtc == null &&
                  roleAssignment.Role == OrganizationRole.Teacher &&
                  roleAssignment.Status == RoleAssignmentStatus.Active &&
                  roleAssignment.EndedAtUtc == null
            select membership.Id)
            .AnyAsync(cancellationToken);

    public Task<TeacherAssignment?> GetActiveTeacherAssignmentAsync(
        Guid organizationId,
        Guid classId,
        Guid teacherMembershipId,
        CancellationToken cancellationToken) =>
        dbContext.TeacherAssignments.SingleOrDefaultAsync(
            assignment => assignment.OrganizationId == organizationId &&
                          assignment.ClassId == classId &&
                          assignment.TeacherMembershipId == teacherMembershipId &&
                          assignment.Status == TeacherAssignmentStatus.Active &&
                          assignment.EndedAtUtc == null,
            cancellationToken);

    public Task<TeacherAssignment?> GetTeacherAssignmentForUpdateAsync(
        Guid organizationId,
        Guid classId,
        Guid teacherAssignmentId,
        CancellationToken cancellationToken) =>
        dbContext.TeacherAssignments
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[TeacherAssignments] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ClassId] = {classId} AND [Id] = {teacherAssignmentId}")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AcademicClassRecord>> GetClassesForManagerAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await (
            from academicClass in dbContext.Classes
            join period in dbContext.AcademicPeriods
                on new { academicClass.OrganizationId, Id = academicClass.AcademicPeriodId }
                equals new { period.OrganizationId, period.Id }
            join course in dbContext.Courses
                on new { academicClass.OrganizationId, Id = academicClass.CourseId }
                equals new { course.OrganizationId, course.Id }
            where academicClass.OrganizationId == organizationId
            orderby academicClass.Title, academicClass.Id
            select new AcademicClassRecord(
                academicClass.Id,
                academicClass.OrganizationId,
                academicClass.AcademicPeriodId,
                period.Title,
                academicClass.CourseId,
                course.Title,
                academicClass.Title,
                academicClass.Capacity,
                academicClass.Status))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AcademicClassRecord>> GetClassesForTeacherAsync(
        Guid organizationId,
        Guid teacherMembershipId,
        CancellationToken cancellationToken) =>
        await (
            from assignment in dbContext.TeacherAssignments
            join academicClass in dbContext.Classes
                on new { assignment.OrganizationId, Id = assignment.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join period in dbContext.AcademicPeriods
                on new { academicClass.OrganizationId, Id = academicClass.AcademicPeriodId }
                equals new { period.OrganizationId, period.Id }
            join course in dbContext.Courses
                on new { academicClass.OrganizationId, Id = academicClass.CourseId }
                equals new { course.OrganizationId, course.Id }
            where assignment.OrganizationId == organizationId &&
                  assignment.TeacherMembershipId == teacherMembershipId &&
                  assignment.Status == TeacherAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null &&
                  academicClass.Status == ClassStatus.Active &&
                  academicClass.EndedAtUtc == null
            orderby academicClass.Title, academicClass.Id
            select new AcademicClassRecord(
                academicClass.Id,
                academicClass.OrganizationId,
                academicClass.AcademicPeriodId,
                period.Title,
                academicClass.CourseId,
                course.Title,
                academicClass.Title,
                academicClass.Capacity,
                academicClass.Status))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<AcademicClassRecord>> GetClassesForStudentAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await (
            from enrollment in dbContext.Enrollments
            join organizationPerson in dbContext.OrganizationPersons
                on new
                {
                    enrollment.OrganizationId,
                    Id = enrollment.LearnerOrganizationPersonId
                }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            from user in dbContext.Users
            join academicClass in dbContext.Classes
                on new { enrollment.OrganizationId, Id = enrollment.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join period in dbContext.AcademicPeriods
                on new { academicClass.OrganizationId, Id = academicClass.AcademicPeriodId }
                equals new { period.OrganizationId, period.Id }
            join course in dbContext.Courses
                on new { academicClass.OrganizationId, Id = academicClass.CourseId }
                equals new { course.OrganizationId, course.Id }
            where user.Id == userId &&
                  user.PersonId == organizationPerson.PersonId &&
                  enrollment.OrganizationId == organizationId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null &&
                  academicClass.Status == ClassStatus.Active &&
                  academicClass.EndedAtUtc == null
            orderby academicClass.Title, academicClass.Id
            select new AcademicClassRecord(
                academicClass.Id,
                academicClass.OrganizationId,
                academicClass.AcademicPeriodId,
                period.Title,
                academicClass.CourseId,
                course.Title,
                academicClass.Title,
                academicClass.Capacity,
                academicClass.Status))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public void Add(AcademicPeriod academicPeriod) => dbContext.AcademicPeriods.Add(academicPeriod);
    public void Add(Course course) => dbContext.Courses.Add(course);
    public void Add(AcademicClass academicClass) => dbContext.Classes.Add(academicClass);
    public void Add(Enrollment enrollment) => dbContext.Enrollments.Add(enrollment);
    public void Add(TeacherAssignment teacherAssignment) =>
        dbContext.TeacherAssignments.Add(teacherAssignment);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AcademicException(
                AcademicErrorCodes.ConcurrencyConflict,
                "اطلاعات هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  sqlException.Message.Contains(
                      "UX_Enrollments_Active_Organization_Class_Learner",
                      StringComparison.Ordinal))
        {
            throw new AcademicException(
                AcademicErrorCodes.EnrollmentAlreadyActive,
                "ثبت‌نام فعال از قبل وجود دارد.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  sqlException.Message.Contains(
                      "UX_TeacherAssignments_Active_Organization_Class_Teacher",
                      StringComparison.Ordinal))
        {
            throw new AcademicException(
                AcademicErrorCodes.TeacherAssignmentAlreadyActive,
                "انتساب فعال معلم از قبل وجود دارد.");
        }
    }

    private sealed class EfAcademicTransaction(IDbContextTransaction transaction)
        : IAcademicTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
