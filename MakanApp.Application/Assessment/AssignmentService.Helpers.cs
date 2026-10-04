using MakanApp.Application.Academic;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Application.Assessment;

public sealed partial class AssignmentService
{
    private async Task<AccessContext> GetOrganizationContextAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(userId, sessionId, cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            !context.ActiveRole.HasValue)
        {
            throw NotAllowed();
        }

        return context;
    }

    private async Task<AcademicClass> GetActiveClassForUpdateAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken)
    {
        var academicClass = await _store.GetClassForUpdateAsync(
            organizationId,
            classId,
            cancellationToken);
        if (academicClass is null)
        {
            throw AssignmentNotFound();
        }

        if (!academicClass.IsActive)
        {
            throw new AssessmentException(
                AcademicErrorCodes.ClassNotActive,
                "کلاس برای تعریف تکلیف فعال نیست.");
        }

        return academicClass;
    }

    private async Task EnsureCanManageClassAsync(
        AccessContext context,
        Guid classId,
        CancellationToken cancellationToken)
    {
        if (context.ActiveRole == OrganizationRole.Manager)
        {
            return;
        }

        if (context.ActiveRole == OrganizationRole.Teacher)
        {
            if (await _store.HasActiveTeacherAssignmentAsync(
                context.OrganizationId!.Value,
                classId,
                context.MembershipId!.Value,
                cancellationToken))
            {
                return;
            }

            throw new AssessmentException(
                AssessmentErrorCodes.TeacherNotAssigned,
                "معلم برای کلاس انتخاب‌شده TeacherAssignment فعال ندارد.");
        }

        throw NotAllowed();
    }

    private async Task<AssignmentWithVersionRecord> GetAssignmentForUpdateAsync(
        Guid organizationId,
        Guid assignmentId,
        CancellationToken cancellationToken) =>
        await _store.GetAssignmentForUpdateAsync(
            organizationId,
            assignmentId,
            cancellationToken) ?? throw AssignmentNotFound();

    private static AssessmentException MapValidationException(Exception exception)
    {
        var code = exception switch
        {
            ArgumentOutOfRangeException { ParamName: "maxAttempts" } => AssessmentErrorCodes.AssignmentAttemptsInvalid,
            ArgumentOutOfRangeException { ParamName: "maxScore" } => AssessmentErrorCodes.AssignmentMaxScoreInvalid,
            ArgumentException { ParamName: "title" } => AssessmentErrorCodes.AssignmentTitleRequired,
            ArgumentException { ParamName: "description" } => AssessmentErrorCodes.AssignmentDescriptionInvalid,
            ArgumentException { ParamName: "dueAtUtc" } => AssessmentErrorCodes.AssignmentDueDateInvalid,
            InvalidOperationException => AssessmentErrorCodes.AssignmentNotDraft,
            _ => AssessmentErrorCodes.AssignmentDueDateInvalid
        };
        return new AssessmentException(code, exception.Message);
    }

    private static bool IsDomainValidationException(Exception exception) =>
        exception is ArgumentException or InvalidOperationException;

    private static byte[] DecodeRowVersion(string value)
    {
        try
        {
            var rowVersion = Convert.FromBase64String(value);
            if (rowVersion.Length != 8)
            {
                throw new FormatException();
            }

            return rowVersion;
        }
        catch (FormatException)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "نسخه رکورد معتبر نیست؛ داده را دوباره دریافت کنید.");
        }
    }

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static AssessmentException AssignmentNotFound() =>
        new(AssessmentErrorCodes.AssignmentNotFound, "تکلیف پیدا نشد.");

    private static AssessmentException NotAllowed() =>
        new(AssessmentErrorCodes.AssignmentNotAllowed, "دسترسی به این تکلیف مجاز نیست.");

    private static AssignmentResult ToResult(
        Assignment assignment,
        AssignmentVersion version,
        string classTitle) =>
        new(
            assignment.Id,
            assignment.OrganizationId,
            assignment.ClassId,
            classTitle,
            assignment.Status,
            assignment.CreatedAtUtc,
            assignment.UpdatedAtUtc,
            assignment.PublishedAtUtc,
            version.Id,
            version.VersionNumber,
            version.Title,
            version.Description,
            version.DueAtUtc,
            version.AllowLateSubmission,
            version.MaxAttempts,
            version.MaxScore,
            Convert.ToBase64String(assignment.RowVersion),
            Convert.ToBase64String(version.RowVersion));
}
