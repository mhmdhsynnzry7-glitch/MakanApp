using MakanApp.Domain.Academic;

namespace MakanApp.Application.Academic;

public sealed record CreateAcademicPeriodCommand(
    string Title,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record AcademicPeriodResult(
    Guid Id,
    Guid OrganizationId,
    string Title,
    DateOnly StartDate,
    DateOnly EndDate,
    AcademicPeriodStatus Status);

public sealed record CreateCourseCommand(string Title);

public sealed record CourseResult(
    Guid Id,
    Guid OrganizationId,
    string Title,
    CourseStatus Status);

public sealed record CreateClassCommand(
    Guid AcademicPeriodId,
    Guid CourseId,
    string Title,
    int Capacity,
    bool ActivateImmediately);

public sealed record ClassResult(
    Guid Id,
    Guid OrganizationId,
    Guid AcademicPeriodId,
    string AcademicPeriodTitle,
    Guid CourseId,
    string CourseTitle,
    string Title,
    int Capacity,
    ClassStatus Status);

public sealed record EnrollLearnerCommand(Guid LearnerOrganizationPersonId);

public sealed record EnrollmentResult(
    Guid Id,
    Guid OrganizationId,
    Guid ClassId,
    Guid LearnerOrganizationPersonId,
    EnrollmentStatus Status,
    DateTime EnrolledAtUtc,
    DateTime? EndedAtUtc);

public sealed record EndEnrollmentCommand(EnrollmentStatus FinalStatus);

public sealed record AssignTeacherCommand(Guid TeacherMembershipId);

public sealed record TeacherAssignmentResult(
    Guid Id,
    Guid OrganizationId,
    Guid ClassId,
    Guid TeacherMembershipId,
    TeacherAssignmentStatus Status,
    DateTime AssignedAtUtc,
    DateTime? EndedAtUtc);

public sealed record AcademicClassRecord(
    Guid Id,
    Guid OrganizationId,
    Guid AcademicPeriodId,
    string AcademicPeriodTitle,
    Guid CourseId,
    string CourseTitle,
    string Title,
    int Capacity,
    ClassStatus Status);
