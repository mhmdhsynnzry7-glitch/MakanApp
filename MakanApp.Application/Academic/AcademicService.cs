using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Academic;

public sealed class AcademicService(
    IAccessContextResolver accessContextResolver,
    IAcademicStore store,
    TimeProvider timeProvider) : IAcademicService
{
    public async Task<AcademicPeriodResult> CreateAcademicPeriodAsync(
        Guid userId,
        Guid sessionId,
        CreateAcademicPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetManagerContextAsync(userId, sessionId, cancellationToken);
        AcademicPeriod academicPeriod;
        try
        {
            academicPeriod = AcademicPeriod.Create(
                context.OrganizationId!.Value,
                command.Title,
                command.StartDate,
                command.EndDate,
                UtcNow());
        }
        catch (ArgumentException exception)
        {
            throw new AcademicException(
                AcademicErrorCodes.AcademicPeriodInvalid,
                exception.Message);
        }

        store.Add(academicPeriod);
        await store.SaveChangesAsync(cancellationToken);
        return ToResult(academicPeriod);
    }

    public async Task<CourseResult> CreateCourseAsync(
        Guid userId,
        Guid sessionId,
        CreateCourseCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetManagerContextAsync(userId, sessionId, cancellationToken);
        Course course;
        try
        {
            course = Course.Create(context.OrganizationId!.Value, command.Title, UtcNow());
        }
        catch (ArgumentException exception)
        {
            throw new AcademicException(AcademicErrorCodes.CourseInvalid, exception.Message);
        }

        store.Add(course);
        await store.SaveChangesAsync(cancellationToken);
        return ToResult(course);
    }

    public async Task<ClassResult> CreateClassAsync(
        Guid userId,
        Guid sessionId,
        CreateClassCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetManagerContextAsync(userId, sessionId, cancellationToken);
        var organizationId = context.OrganizationId!.Value;
        var period = await store.GetAcademicPeriodAsync(
            organizationId,
            command.AcademicPeriodId,
            cancellationToken);
        if (period is null || !period.IsActive)
        {
            throw NotFound(AcademicErrorCodes.AcademicPeriodNotFound, "دوره تحصیلی معتبر پیدا نشد.");
        }

        var course = await store.GetCourseAsync(organizationId, command.CourseId, cancellationToken);
        if (course is null || !course.IsActive)
        {
            throw NotFound(AcademicErrorCodes.CourseNotFound, "درس معتبر پیدا نشد.");
        }

        Class academicClass;
        try
        {
            academicClass = Class.CreateDraft(
                organizationId,
                period.Id,
                course.Id,
                command.Title,
                command.Capacity,
                UtcNow());
        }
        catch (ArgumentException exception)
        {
            throw new AcademicException(AcademicErrorCodes.ClassInvalid, exception.Message);
        }

        if (command.ActivateImmediately)
        {
            academicClass.Activate(UtcNow());
        }

        store.Add(academicClass);
        await store.SaveChangesAsync(cancellationToken);
        return ToResult(academicClass, period.Title, course.Title);
    }

    public async Task<EnrollmentResult> EnrollLearnerAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        EnrollLearnerCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetManagerContextAsync(userId, sessionId, cancellationToken);
        var organizationId = context.OrganizationId!.Value;
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var academicClass = await store.GetClassForUpdateAsync(
            organizationId,
            classId,
            cancellationToken);
        if (academicClass is null)
        {
            throw ClassNotFound();
        }

        if (!academicClass.IsActive)
        {
            throw new AcademicException(
                AcademicErrorCodes.ClassNotActive,
                "کلاس برای ثبت‌نام فعال نیست.");
        }

        var learner = await store.GetActiveOrganizationPersonAsync(
            organizationId,
            command.LearnerOrganizationPersonId,
            cancellationToken);
        if (learner is null)
        {
            throw NotFound(AcademicErrorCodes.LearnerNotFound, "فراگیر معتبر پیدا نشد.");
        }

        var existing = await store.GetActiveEnrollmentAsync(
            organizationId,
            classId,
            learner.Id,
            cancellationToken);
        if (existing is not null)
        {
            throw new AcademicException(
                AcademicErrorCodes.EnrollmentAlreadyActive,
                "ثبت‌نام فعال از قبل وجود دارد.");
        }

        var activeCount = await store.CountActiveEnrollmentsAsync(
            organizationId,
            classId,
            cancellationToken);
        if (activeCount >= academicClass.Capacity)
        {
            throw new AcademicException(
                AcademicErrorCodes.ClassCapacityExceeded,
                "ظرفیت کلاس تکمیل شده است.");
        }

        var enrollment = Enrollment.CreateActive(
            organizationId,
            classId,
            learner.Id,
            UtcNow());
        store.Add(enrollment);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(enrollment);
    }

    public async Task<EnrollmentResult> EndEnrollmentAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        Guid enrollmentId,
        EndEnrollmentCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetManagerContextAsync(userId, sessionId, cancellationToken);
        if (command.FinalStatus is not EnrollmentStatus.Completed and not EnrollmentStatus.Withdrawn)
        {
            throw new AcademicException(
                AcademicErrorCodes.EnrollmentNotActive,
                "وضعیت پایان ثبت‌نام معتبر نیست.");
        }

        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var academicClass = await store.GetClassForUpdateAsync(
            context.OrganizationId!.Value,
            classId,
            cancellationToken);
        if (academicClass is null)
        {
            throw ClassNotFound();
        }

        var enrollment = await store.GetEnrollmentForUpdateAsync(
            context.OrganizationId.Value,
            classId,
            enrollmentId,
            cancellationToken);
        if (enrollment is null || !enrollment.IsActive)
        {
            throw new AcademicException(
                AcademicErrorCodes.EnrollmentNotActive,
                "ثبت‌نام فعال پیدا نشد.");
        }

        if (command.FinalStatus == EnrollmentStatus.Completed)
        {
            enrollment.Complete(UtcNow());
        }
        else
        {
            enrollment.Withdraw(UtcNow());
        }

        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(enrollment);
    }

    public async Task<TeacherAssignmentResult> AssignTeacherAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        AssignTeacherCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetManagerContextAsync(userId, sessionId, cancellationToken);
        var organizationId = context.OrganizationId!.Value;
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var academicClass = await store.GetClassForUpdateAsync(
            organizationId,
            classId,
            cancellationToken);
        if (academicClass is null)
        {
            throw ClassNotFound();
        }

        if (!await store.HasActiveTeacherAuthorityAsync(
                organizationId,
                command.TeacherMembershipId,
                cancellationToken))
        {
            throw new AcademicException(
                AcademicErrorCodes.TeacherNotAllowed,
                "عضویت معلم معتبر نیست یا نقش فعال معلم ندارد.");
        }

        var existing = await store.GetActiveTeacherAssignmentAsync(
            organizationId,
            classId,
            command.TeacherMembershipId,
            cancellationToken);
        if (existing is not null)
        {
            throw new AcademicException(
                AcademicErrorCodes.TeacherAssignmentAlreadyActive,
                "انتساب فعال معلم از قبل وجود دارد.");
        }

        var assignment = TeacherAssignment.CreateActive(
            organizationId,
            classId,
            command.TeacherMembershipId,
            UtcNow());
        store.Add(assignment);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(assignment);
    }

    public async Task<TeacherAssignmentResult> EndTeacherAssignmentAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        Guid teacherAssignmentId,
        CancellationToken cancellationToken)
    {
        var context = await GetManagerContextAsync(userId, sessionId, cancellationToken);
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var academicClass = await store.GetClassForUpdateAsync(
            context.OrganizationId!.Value,
            classId,
            cancellationToken);
        if (academicClass is null)
        {
            throw ClassNotFound();
        }

        var assignment = await store.GetTeacherAssignmentForUpdateAsync(
            context.OrganizationId.Value,
            classId,
            teacherAssignmentId,
            cancellationToken);
        if (assignment is null || !assignment.IsActive)
        {
            throw new AcademicException(
                AcademicErrorCodes.TeacherAssignmentNotActive,
                "انتساب فعال معلم پیدا نشد.");
        }

        assignment.End(UtcNow());
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(assignment);
    }

    public async Task<IReadOnlyCollection<ClassResult>> GetClassesAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            !context.ActiveRole.HasValue)
        {
            throw AcademicReadNotAllowed();
        }

        var records = context.ActiveRole.Value switch
        {
            OrganizationRole.Manager => await store.GetClassesForManagerAsync(
                context.OrganizationId.Value,
                cancellationToken),
            OrganizationRole.Teacher => await store.GetClassesForTeacherAsync(
                context.OrganizationId.Value,
                context.MembershipId.Value,
                cancellationToken),
            OrganizationRole.Student => await store.GetClassesForStudentAsync(
                context.OrganizationId.Value,
                userId,
                cancellationToken),
            _ => throw AcademicReadNotAllowed()
        };

        return records.Select(ToResult).ToArray();
    }

    private async Task<AccessContext> GetManagerContextAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            context.ActiveRole != OrganizationRole.Manager)
        {
            throw new AcademicException(
                AcademicErrorCodes.ManagerRoleRequired,
                "فضای سازمانی با نقش فعال مدیر لازم است.");
        }

        return context;
    }

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static AcademicException ClassNotFound() =>
        NotFound(AcademicErrorCodes.ClassNotFound, "کلاس پیدا نشد.");

    private static AcademicException AcademicReadNotAllowed() =>
        new(
            AcademicErrorCodes.AcademicReadNotAllowed,
            "دسترسی خواندن کلاس‌ها برای بافت فعلی مجاز نیست.");

    private static AcademicException NotFound(string code, string message) => new(code, message);

    private static AcademicPeriodResult ToResult(AcademicPeriod period) =>
        new(
            period.Id,
            period.OrganizationId,
            period.Title,
            period.StartDate,
            period.EndDate,
            period.Status);

    private static CourseResult ToResult(Course course) =>
        new(course.Id, course.OrganizationId, course.Title, course.Status);

    private static ClassResult ToResult(
        Class academicClass,
        string periodTitle,
        string courseTitle) =>
        new(
            academicClass.Id,
            academicClass.OrganizationId,
            academicClass.AcademicPeriodId,
            periodTitle,
            academicClass.CourseId,
            courseTitle,
            academicClass.Title,
            academicClass.Capacity,
            academicClass.Status);

    private static ClassResult ToResult(AcademicClassRecord record) =>
        new(
            record.Id,
            record.OrganizationId,
            record.AcademicPeriodId,
            record.AcademicPeriodTitle,
            record.CourseId,
            record.CourseTitle,
            record.Title,
            record.Capacity,
            record.Status);

    private static EnrollmentResult ToResult(Enrollment enrollment) =>
        new(
            enrollment.Id,
            enrollment.OrganizationId,
            enrollment.ClassId,
            enrollment.LearnerOrganizationPersonId,
            enrollment.Status,
            enrollment.EnrolledAtUtc,
            enrollment.EndedAtUtc);

    private static TeacherAssignmentResult ToResult(TeacherAssignment assignment) =>
        new(
            assignment.Id,
            assignment.OrganizationId,
            assignment.ClassId,
            assignment.TeacherMembershipId,
            assignment.Status,
            assignment.AssignedAtUtc,
            assignment.EndedAtUtc);
}
