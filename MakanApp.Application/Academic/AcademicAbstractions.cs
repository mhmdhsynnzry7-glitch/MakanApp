using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Academic;

public interface IAcademicService
{
    Task<AcademicPeriodResult> CreateAcademicPeriodAsync(
        Guid userId,
        Guid sessionId,
        CreateAcademicPeriodCommand command,
        CancellationToken cancellationToken);

    Task<CourseResult> CreateCourseAsync(
        Guid userId,
        Guid sessionId,
        CreateCourseCommand command,
        CancellationToken cancellationToken);

    Task<ClassResult> CreateClassAsync(
        Guid userId,
        Guid sessionId,
        CreateClassCommand command,
        CancellationToken cancellationToken);

    Task<EnrollmentResult> EnrollLearnerAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        EnrollLearnerCommand command,
        CancellationToken cancellationToken);

    Task<EnrollmentResult> EndEnrollmentAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        Guid enrollmentId,
        EndEnrollmentCommand command,
        CancellationToken cancellationToken);

    Task<TeacherAssignmentResult> AssignTeacherAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        AssignTeacherCommand command,
        CancellationToken cancellationToken);

    Task<TeacherAssignmentResult> EndTeacherAssignmentAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        Guid teacherAssignmentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ClassResult>> GetClassesAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);
}

public interface IAcademicTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IAcademicStore
{
    Task<IAcademicTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken);

    Task<AcademicPeriod?> GetAcademicPeriodAsync(
        Guid organizationId,
        Guid academicPeriodId,
        CancellationToken cancellationToken);

    Task<Course?> GetCourseAsync(
        Guid organizationId,
        Guid courseId,
        CancellationToken cancellationToken);

    Task<Class?> GetClassForUpdateAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken);

    Task<OrganizationPerson?> GetActiveOrganizationPersonAsync(
        Guid organizationId,
        Guid organizationPersonId,
        CancellationToken cancellationToken);

    Task<Enrollment?> GetActiveEnrollmentAsync(
        Guid organizationId,
        Guid classId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken);

    Task<Enrollment?> GetEnrollmentForUpdateAsync(
        Guid organizationId,
        Guid classId,
        Guid enrollmentId,
        CancellationToken cancellationToken);

    Task<int> CountActiveEnrollmentsAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken);

    Task<bool> HasActiveTeacherAuthorityAsync(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken);

    Task<TeacherAssignment?> GetActiveTeacherAssignmentAsync(
        Guid organizationId,
        Guid classId,
        Guid teacherMembershipId,
        CancellationToken cancellationToken);

    Task<TeacherAssignment?> GetTeacherAssignmentForUpdateAsync(
        Guid organizationId,
        Guid classId,
        Guid teacherAssignmentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AcademicClassRecord>> GetClassesForManagerAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AcademicClassRecord>> GetClassesForTeacherAsync(
        Guid organizationId,
        Guid teacherMembershipId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AcademicClassRecord>> GetClassesForStudentAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);

    void Add(AcademicPeriod academicPeriod);
    void Add(Course course);
    void Add(Class academicClass);
    void Add(Enrollment enrollment);
    void Add(TeacherAssignment teacherAssignment);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
