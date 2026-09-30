using MakanApp.Domain.Academic;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<Guid> CreateAcademicPeriodAsync(Guid organizationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var period = AcademicPeriod.Create(
            organizationId,
            $"Period {Guid.NewGuid():N}",
            new DateOnly(2026, 9, 1),
            new DateOnly(2027, 6, 30),
            DateTime.UtcNow);
        dbContext.AcademicPeriods.Add(period);
        await dbContext.SaveChangesAsync();
        return period.Id;
    }

    public async Task<Guid> CreateCourseAsync(Guid organizationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var course = Course.Create(
            organizationId,
            $"Course {Guid.NewGuid():N}",
            DateTime.UtcNow);
        dbContext.Courses.Add(course);
        await dbContext.SaveChangesAsync();
        return course.Id;
    }

    public async Task<Guid> CreateClassAsync(
        Guid organizationId,
        Guid academicPeriodId,
        Guid courseId,
        int capacity = 20,
        bool active = true)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var academicClass = AcademicClass.CreateDraft(
            organizationId,
            academicPeriodId,
            courseId,
            $"Class {Guid.NewGuid():N}",
            capacity,
            DateTime.UtcNow);
        if (active)
        {
            academicClass.Activate(DateTime.UtcNow);
        }

        dbContext.Classes.Add(academicClass);
        await dbContext.SaveChangesAsync();
        return academicClass.Id;
    }

    public async Task<(Guid PeriodId, Guid CourseId, Guid ClassId)> CreateAcademicClassAsync(
        Guid organizationId,
        int capacity = 20,
        bool active = true)
    {
        var periodId = await CreateAcademicPeriodAsync(organizationId);
        var courseId = await CreateCourseAsync(organizationId);
        var classId = await CreateClassAsync(
            organizationId,
            periodId,
            courseId,
            capacity,
            active);
        return (periodId, courseId, classId);
    }

    public async Task<Guid[]> CreateOrganizationPersonsAsync(Guid organizationId, int count)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        var organizationPersons = new List<OrganizationPerson>(count);
        for (var index = 0; index < count; index++)
        {
            var person = Person.Create(
                "Learner",
                $"{index}-{Guid.NewGuid():N}",
                $"Learner {index}",
                nowUtc);
            var organizationPerson = OrganizationPerson.CreateActive(
                organizationId,
                person.Id,
                nowUtc);
            dbContext.Persons.Add(person);
            organizationPersons.Add(organizationPerson);
        }

        dbContext.OrganizationPersons.AddRange(organizationPersons);
        await dbContext.SaveChangesAsync();
        return organizationPersons.Select(item => item.Id).ToArray();
    }

    public async Task<Guid> CreateEnrollmentAsync(
        Guid organizationId,
        Guid classId,
        Guid learnerOrganizationPersonId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var enrollment = Enrollment.CreateActive(
            organizationId,
            classId,
            learnerOrganizationPersonId,
            DateTime.UtcNow);
        dbContext.Enrollments.Add(enrollment);
        await dbContext.SaveChangesAsync();
        return enrollment.Id;
    }

    public async Task<Guid> CreateTeacherAssignmentAsync(
        Guid organizationId,
        Guid classId,
        Guid teacherMembershipId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var assignment = TeacherAssignment.CreateActive(
            organizationId,
            classId,
            teacherMembershipId,
            DateTime.UtcNow);
        dbContext.TeacherAssignments.Add(assignment);
        await dbContext.SaveChangesAsync();
        return assignment.Id;
    }

    public async Task<int> CountActiveEnrollmentsAsync(Guid organizationId, Guid classId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Enrollments.CountAsync(enrollment =>
            enrollment.OrganizationId == organizationId &&
            enrollment.ClassId == classId &&
            enrollment.Status == EnrollmentStatus.Active &&
            enrollment.EndedAtUtc == null);
    }

    public async Task<int> CountEnrollmentsAsync(
        Guid organizationId,
        Guid classId,
        Guid learnerOrganizationPersonId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Enrollments.CountAsync(enrollment =>
            enrollment.OrganizationId == organizationId &&
            enrollment.ClassId == classId &&
            enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId);
    }

    public async Task<EnrollmentStatus> GetEnrollmentStatusAsync(Guid enrollmentId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Enrollments
            .Where(enrollment => enrollment.Id == enrollmentId)
            .Select(enrollment => enrollment.Status)
            .SingleAsync();
    }

    public async Task<TeacherAssignmentStatus> GetTeacherAssignmentStatusAsync(Guid assignmentId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.TeacherAssignments
            .Where(assignment => assignment.Id == assignmentId)
            .Select(assignment => assignment.Status)
            .SingleAsync();
    }

    public async Task<Guid> GetUserPersonIdAsync(Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => user.PersonId!.Value)
            .SingleAsync();
    }

    public async Task<bool> CrossOrganizationClassReferencesAreRejectedAsync(
        Guid organizationId,
        Guid academicPeriodId,
        Guid courseId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        dbContext.Classes.Add(AcademicClass.CreateDraft(
            organizationId,
            academicPeriodId,
            courseId,
            "Invalid class",
            10,
            DateTime.UtcNow));
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

    public async Task<bool> CrossOrganizationEnrollmentIsRejectedAsync(
        Guid organizationId,
        Guid classId,
        Guid learnerOrganizationPersonId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        dbContext.Enrollments.Add(Enrollment.CreateActive(
            organizationId,
            classId,
            learnerOrganizationPersonId,
            DateTime.UtcNow));
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

    public async Task<bool> CrossOrganizationTeacherAssignmentIsRejectedAsync(
        Guid organizationId,
        Guid classId,
        Guid teacherMembershipId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        dbContext.TeacherAssignments.Add(TeacherAssignment.CreateActive(
            organizationId,
            classId,
            teacherMembershipId,
            DateTime.UtcNow));
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

    public async Task<bool> DuplicateActiveEnrollmentIsRejectedAsync(
        Guid organizationId,
        Guid classId,
        Guid learnerOrganizationPersonId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        dbContext.Enrollments.AddRange(
            Enrollment.CreateActive(organizationId, classId, learnerOrganizationPersonId, nowUtc),
            Enrollment.CreateActive(organizationId, classId, learnerOrganizationPersonId, nowUtc));
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

    public async Task<bool> DuplicateActiveTeacherAssignmentIsRejectedAsync(
        Guid organizationId,
        Guid classId,
        Guid teacherMembershipId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        dbContext.TeacherAssignments.AddRange(
            TeacherAssignment.CreateActive(organizationId, classId, teacherMembershipId, nowUtc),
            TeacherAssignment.CreateActive(organizationId, classId, teacherMembershipId, nowUtc));
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
