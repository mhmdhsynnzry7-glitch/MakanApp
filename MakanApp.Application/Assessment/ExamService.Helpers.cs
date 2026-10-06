using MakanApp.Application.Academic;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamService
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

    private async Task<ExamAggregateRecord> GetCurrentForUpdateAsync(
        AccessContext context,
        Guid examId,
        CancellationToken cancellationToken)
    {
        var record = await _store.GetCurrentForUpdateAsync(
            context.OrganizationId!.Value,
            examId,
            cancellationToken) ?? throw ExamNotFound();
        await EnsureCanManageClassAsync(context, record.Exam.ClassId, cancellationToken);
        return record;
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
                "معلم برای کلاس آزمون TeacherAssignment فعال ندارد.");
        }

        throw NotAllowed();
    }

    private static void EnsureClassActive(bool isActive)
    {
        if (!isActive)
        {
            throw new AssessmentException(
                AcademicErrorCodes.ClassNotActive,
                "کلاس برای تعریف یا انتشار آزمون فعال نیست.");
        }
    }

    private static void EnsureDraft(ExamVersion version)
    {
        if (!version.IsDraft)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamVersionLocked,
                "نسخه منتشرشده آزمون تغییرپذیر نیست.");
        }
    }

    private static IReadOnlyCollection<ExamQuestionOptionDefinition>? ToDefinitions(
        IReadOnlyCollection<ExamQuestionOptionCommand>? options) =>
        options?.Select(option => new ExamQuestionOptionDefinition(
            option.Order,
            option.Text,
            option.IsCorrect)).ToArray();

    private static AssessmentException MapValidationException(Exception exception)
    {
        var code = exception switch
        {
            ExamQuestionRequiredException => AssessmentErrorCodes.ExamQuestionRequired,
            ExamScoreTotalInvalidException => AssessmentErrorCodes.ExamScoreTotalInvalid,
            ExamAnswerKeyInvalidException => AssessmentErrorCodes.ExamAnswerKeyInvalid,
            ArgumentOutOfRangeException { ParamName: "durationMinutes" } => AssessmentErrorCodes.ExamDurationInvalid,
            ArgumentOutOfRangeException { ParamName: "maxAttempts" } => AssessmentErrorCodes.ExamAttemptsInvalid,
            ArgumentOutOfRangeException { ParamName: "maxScore" } => AssessmentErrorCodes.ExamMaxScoreInvalid,
            ArgumentOutOfRangeException { ParamName: "randomizationPolicy" } => AssessmentErrorCodes.ExamRandomizationInvalid,
            ArgumentOutOfRangeException { ParamName: "order" } => AssessmentErrorCodes.ExamQuestionOrderInvalid,
            ArgumentOutOfRangeException { ParamName: "score" } => AssessmentErrorCodes.ExamQuestionScoreInvalid,
            ArgumentException { ParamName: "title" } => AssessmentErrorCodes.ExamTitleRequired,
            ArgumentException { ParamName: "prompt" } => AssessmentErrorCodes.ExamQuestionRequired,
            ArgumentException { ParamName: "availableFromUtc" or "availableUntilUtc" } => AssessmentErrorCodes.ExamWindowInvalid,
            InvalidOperationException => AssessmentErrorCodes.ExamVersionLocked,
            _ => AssessmentErrorCodes.ExamWindowInvalid
        };
        return new AssessmentException(code, exception.Message);
    }

    private static bool IsDomainValidationException(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or
            ExamQuestionRequiredException or ExamScoreTotalInvalidException or ExamAnswerKeyInvalidException;

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

    private static AssessmentException ExamNotFound() =>
        new(AssessmentErrorCodes.ExamNotFound, "آزمون پیدا نشد.");

    private static AssessmentException NotAllowed() =>
        new(AssessmentErrorCodes.ExamNotAllowed, "دسترسی به آزمون مجاز نیست.");

    private static ExamEditorDto ToEditor(ExamAggregateRecord record) =>
        new(
            record.Exam.Id,
            record.Exam.OrganizationId,
            record.Exam.ClassId,
            record.ClassTitle,
            record.Exam.Status,
            record.Exam.CreatedAtUtc,
            Convert.ToBase64String(record.Exam.RowVersion),
            record.Version.Id,
            record.Version.VersionNumber,
            record.Version.Status,
            record.Version.Title,
            record.Version.Description,
            record.Version.AvailableFromUtc,
            record.Version.AvailableUntilUtc,
            record.Version.DurationMinutes,
            record.Version.MaxAttempts,
            record.Version.MaxScore,
            record.Version.RandomizationPolicy,
            record.Version.PublishedAtUtc,
            Convert.ToBase64String(record.Version.RowVersion),
            record.Questions
                .OrderBy(question => question.Order)
                .Select(question => new ExamEditorQuestionDto(
                    question.Id,
                    question.Order,
                    question.Type,
                    question.Prompt,
                    question.Score,
                    question.Options
                        .OrderBy(option => option.Order)
                        .Select(option => new ExamEditorOptionDto(
                            option.Id,
                            option.Order,
                            option.Text,
                            option.IsCorrect))
                        .ToArray(),
                    Convert.ToBase64String(question.RowVersion)))
                .ToArray());

    private static StudentExamSummary ToSummary(ExamSummaryRecord record) =>
        new(
            record.Exam.Id,
            record.Version.Id,
            record.Exam.ClassId,
            record.ClassTitle,
            record.Version.VersionNumber,
            record.Version.Title,
            record.Version.Description,
            record.Version.Status,
            new StudentExamRules(
                record.Version.AvailableFromUtc,
                record.Version.AvailableUntilUtc,
                record.Version.DurationMinutes,
                record.Version.MaxAttempts,
                record.Version.MaxScore,
                record.Version.RandomizationPolicy));

    private static StudentSafeExamPreview ToStudentSafePreview(ExamAggregateRecord record) =>
        new(
            ToSummary(new ExamSummaryRecord(record.Exam, record.Version, record.ClassTitle)),
            record.Questions
                .OrderBy(question => question.Order)
                .Select(question => new StudentSafeExamQuestion(
                    question.Id,
                    question.Order,
                    question.Type,
                    question.Prompt,
                    question.Score,
                    question.Options
                        .OrderBy(option => option.Order)
                        .Select(option => new StudentSafeExamOption(
                            option.Id,
                            option.Order,
                            option.Text))
                        .ToArray()))
                .ToArray());

    private static ExamTeacherPreview ToTeacherPreview(ExamAggregateRecord record)
    {
        var editor = ToEditor(record);
        return new ExamTeacherPreview(
            ToSummary(new ExamSummaryRecord(record.Exam, record.Version, record.ClassTitle)),
            editor.Questions);
    }
}
