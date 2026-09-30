using MakanApp.Domain.Academic;
using Xunit;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.UnitTests.Academic;

public sealed class AcademicModelTests
{
    [Fact]
    public void AcademicPeriodRejectsInvalidDateRange()
    {
        var date = new DateOnly(2026, 9, 1);

        Assert.Throws<ArgumentException>(() => AcademicPeriod.Create(
            Guid.NewGuid(),
            "پاییز",
            date,
            date,
            DateTime.UtcNow));
    }

    [Fact]
    public void ClassCapacityMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AcademicClass.CreateDraft(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ریاضی هفتم",
            0,
            DateTime.UtcNow));
    }

    [Fact]
    public void ActiveEnrollmentConsumesSeat()
    {
        var enrollment = CreateEnrollment(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(enrollment.IsActive);
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
    }

    [Fact]
    public void CompletedEnrollmentDoesNotRemainActive()
    {
        var enrollment = CreateEnrollment(Guid.NewGuid(), Guid.NewGuid());

        enrollment.Complete(DateTime.UtcNow);

        Assert.False(enrollment.IsActive);
        Assert.Equal(EnrollmentStatus.Completed, enrollment.Status);
    }

    [Fact]
    public void EndingEnrollmentPreservesIdentityAndHistory()
    {
        var classId = Guid.NewGuid();
        var learnerId = Guid.NewGuid();
        var enrollment = CreateEnrollment(classId, learnerId);
        var enrollmentId = enrollment.Id;

        enrollment.Withdraw(DateTime.UtcNow);

        Assert.Equal(enrollmentId, enrollment.Id);
        Assert.Equal(classId, enrollment.ClassId);
        Assert.Equal(learnerId, enrollment.LearnerOrganizationPersonId);
        Assert.NotNull(enrollment.EndedAtUtc);
    }

    [Fact]
    public void TeacherAssignmentLifecycleEndsAuthority()
    {
        var classId = Guid.NewGuid();
        var assignment = TeacherAssignment.CreateActive(
            Guid.NewGuid(),
            classId,
            Guid.NewGuid(),
            DateTime.UtcNow);

        assignment.End(DateTime.UtcNow);

        Assert.False(assignment.IsActive);
        Assert.False(assignment.GrantsAccessTo(classId));
        Assert.Equal(TeacherAssignmentStatus.Ended, assignment.Status);
    }

    [Fact]
    public void EndingTeacherAssignmentPreservesHistory()
    {
        var membershipId = Guid.NewGuid();
        var assignment = TeacherAssignment.CreateActive(
            Guid.NewGuid(),
            Guid.NewGuid(),
            membershipId,
            DateTime.UtcNow);
        var assignmentId = assignment.Id;

        assignment.End(DateTime.UtcNow);

        Assert.Equal(assignmentId, assignment.Id);
        Assert.Equal(membershipId, assignment.TeacherMembershipId);
        Assert.NotNull(assignment.EndedAtUtc);
    }

    [Fact]
    public void AssignmentDoesNotGrantAccessToUnrelatedClass()
    {
        var assignment = TeacherAssignment.CreateActive(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        Assert.False(assignment.GrantsAccessTo(Guid.NewGuid()));
    }

    [Fact]
    public void MovingLearnerCreatesNewEnrollmentAndPreservesOldOne()
    {
        var learnerId = Guid.NewGuid();
        var oldEnrollment = CreateEnrollment(Guid.NewGuid(), learnerId);
        oldEnrollment.Withdraw(DateTime.UtcNow);
        var newEnrollment = CreateEnrollment(Guid.NewGuid(), learnerId);

        Assert.NotEqual(oldEnrollment.Id, newEnrollment.Id);
        Assert.NotEqual(oldEnrollment.ClassId, newEnrollment.ClassId);
        Assert.Equal(EnrollmentStatus.Withdrawn, oldEnrollment.Status);
        Assert.Equal(EnrollmentStatus.Active, newEnrollment.Status);
    }

    private static Enrollment CreateEnrollment(Guid classId, Guid learnerId) =>
        Enrollment.CreateActive(
            Guid.NewGuid(),
            classId,
            learnerId,
            DateTime.UtcNow);
}
