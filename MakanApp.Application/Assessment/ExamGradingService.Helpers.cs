using System.Security.Cryptography;
using System.Text;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class ExamGradingService
{
    private async Task<AccessContext> GetOrganizationContextAsync(
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
            throw GradeNotAllowed();
        }

        return context;
    }

    private async Task EnsureCanGradeAsync(
        AccessContext context,
        ExamGradingSourceRecord source,
        CancellationToken cancellationToken)
    {
        if (context.ActiveRole == OrganizationRole.Manager)
        {
            return;
        }

        if (context.ActiveRole == OrganizationRole.Teacher)
        {
            if (await store.HasActiveTeacherAssignmentAsync(
                source.Attempt.OrganizationId,
                source.Exam.ClassId,
                context.MembershipId!.Value,
                cancellationToken))
            {
                return;
            }

            throw new AssessmentException(
                AssessmentErrorCodes.TeacherNotAssigned,
                "معلم برای کلاس این آزمون TeacherAssignment فعال ندارد.");
        }

        throw GradeNotAllowed();
    }

    private static void EnsureFinalized(ExamAttempt attempt)
    {
        if (attempt.Status != ExamAttemptStatus.Finalized ||
            !attempt.FinalizedAtUtc.HasValue ||
            !attempt.FinalizedAnswerSetVersion.HasValue)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamGradeAttemptNotFinalized,
                "فقط Attempt نهایی‌شده قابل تصحیح است.");
        }
    }

    private async Task<ExamGradeAggregateRecord> GetAggregateAsync(
        AccessContext context,
        Guid attemptId,
        CancellationToken cancellationToken) =>
        await store.GetAggregateAsync(
            context.OrganizationId!.Value,
            attemptId,
            cancellationToken) ?? throw GradeNotFound();

    private async Task<ExamGradeAggregateRecord> GetAggregateForUpdateAsync(
        AccessContext context,
        Guid attemptId,
        CancellationToken cancellationToken) =>
        await store.GetAggregateForUpdateAsync(
            context.OrganizationId!.Value,
            attemptId,
            cancellationToken) ?? throw GradeNotFound();

    private ExamGradeRevision RequireMutableRevision(ExamGradeAggregateRecord aggregate)
    {
        var revision = aggregate.LatestRevision ?? throw GradeNotFound();
        if (!revision.IsMutable)
        {
            throw AlreadyReleased();
        }

        return revision;
    }

    private void ApplyExpectedRowVersion(ExamGradeRevision revision, string? encodedRowVersion)
    {
        if (string.IsNullOrWhiteSpace(encodedRowVersion))
        {
            throw ConcurrencyConflict();
        }

        var rowVersion = DecodeRowVersion(encodedRowVersion);
        if (!revision.RowVersion.SequenceEqual(rowVersion))
        {
            throw ConcurrencyConflict();
        }

        store.SetOriginalRowVersion(revision, rowVersion);
    }

    private static byte[] DecodeRowVersion(string value)
    {
        try
        {
            var rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length == 8 ? rowVersion : throw new FormatException();
        }
        catch (FormatException)
        {
            throw ConcurrencyConflict();
        }
    }

    private static AssessmentException MapValidationException(Exception exception) =>
        exception switch
        {
            ArgumentOutOfRangeException { ParamName: "awardedScore" or "totalScore" } =>
                new AssessmentException(
                    AssessmentErrorCodes.ExamGradeScoreInvalid,
                    "نمره باید در بازه مجاز سؤال و با حداکثر دو رقم اعشار باشد."),
            ArgumentException { ParamName: "correctionReason" } =>
                new AssessmentException(
                    AssessmentErrorCodes.ExamGradeCorrectionReasonRequired,
                    exception.Message),
            InvalidOperationException => new AssessmentException(
                AssessmentErrorCodes.ExamGradeScoreInvalid,
                exception.Message),
            _ => new AssessmentException(
                AssessmentErrorCodes.ExamGradeIncomplete,
                exception.Message)
        };

    private static string ComputeReleaseRequestHash(Guid attemptId, string expectedRowVersion)
    {
        var canonical = string.Join('|', attemptId.ToString("N"), expectedRowVersion.Trim());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static ExamGradeDto ToGradeDto(
        ExamGradeAggregateRecord aggregate,
        ExamGradeRevision revision,
        IReadOnlyCollection<ExamQuestionGrade> questionGrades)
    {
        var sourceByQuestion = aggregate.Source.Questions.ToDictionary(
            item => item.AttemptQuestion.Id);
        var questionDtos = questionGrades
            .OrderBy(grade => sourceByQuestion[grade.ExamAttemptQuestionId].AttemptQuestion.DisplayOrder)
            .Select(grade =>
            {
                var source = sourceByQuestion[grade.ExamAttemptQuestionId];
                var selectedOptionText = source.FinalRevision?.SelectedOptionId is Guid selectedOptionId
                    ? source.Question.Options.Single(option => option.Id == selectedOptionId).Text
                    : null;
                return new ExamQuestionGradeDto(
                    grade.ExamAttemptQuestionId,
                    grade.QuestionVersionId,
                    source.AttemptQuestion.DisplayOrder,
                    source.Question.Type,
                    source.Question.Prompt,
                    grade.IsAnswered,
                    selectedOptionText,
                    source.FinalRevision?.TextAnswer,
                    grade.AwardedScore,
                    grade.MaximumScore,
                    grade.GradingMode,
                    grade.IsReviewed,
                    grade.LearnerFeedback,
                    grade.EvaluatorPrivateNote,
                    grade.ReviewedByMembershipId,
                    grade.ReviewedAtUtc);
            })
            .ToArray();
        var release = aggregate.Releases.SingleOrDefault(
            item => item.ExamGradeRevisionId == revision.Id);
        return new ExamGradeDto(
            revision.Id,
            aggregate.Source.Attempt.Id,
            aggregate.Source.Exam.Id,
            aggregate.Source.Version.Id,
            aggregate.Source.Exam.ClassId,
            aggregate.Source.Version.Title,
            aggregate.Source.StudentDisplayName,
            aggregate.Source.Attempt.AttemptNumber,
            aggregate.Source.Attempt.FinalizedAtUtc!.Value,
            revision.RevisionNumber,
            revision.TotalScore,
            aggregate.Source.Version.MaxScore,
            revision.Status,
            revision.LearnerFeedback,
            revision.GuardianVisibleFeedback,
            revision.EvaluatorPrivateNote,
            revision.CreatedByMembershipId,
            revision.CreatedAtUtc,
            revision.UpdatedAtUtc,
            revision.SupersedesExamGradeRevisionId,
            revision.CorrectionReason,
            release?.ReleasedAtUtc,
            Convert.ToBase64String(revision.RowVersion),
            questionDtos);
    }

    private static ExamGradeReleaseDto ToReleaseDto(
        ExamGradeRelease release,
        ExamGradeRevision revision,
        decimal maximumScore) =>
        new(
            release.Id,
            release.ExamAttemptId,
            revision.Id,
            revision.RevisionNumber,
            revision.TotalScore,
            maximumScore,
            release.ReleasedAtUtc,
            release.ReleasedByMembershipId,
            Convert.ToBase64String(revision.RowVersion));

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static AssessmentException GradeNotFound() =>
        new(AssessmentErrorCodes.ExamGradeNotFound, "نمره آزمون پیدا نشد.");

    private static AssessmentException GradeNotAllowed() =>
        new(AssessmentErrorCodes.ExamGradeNotAllowed, "دسترسی به نمره آزمون مجاز نیست.");

    private static AssessmentException AlreadyReleased() =>
        new(
            AssessmentErrorCodes.ExamGradeAlreadyReleased,
            "نمره منتشرشده قابل ویرایش مستقیم نیست.");

    private static AssessmentException GradeNotReleased() =>
        new(AssessmentErrorCodes.ExamGradeNotReleased, "نمره منتشرشده پیدا نشد.");

    private static AssessmentException ReleaseConflict() =>
        new(
            AssessmentErrorCodes.ExamGradeReleaseConflict,
            "وضعیت انتشار نمره هم‌زمان تغییر کرده است.");

    private static AssessmentException ReleaseIdempotencyConflict() =>
        new(
            AssessmentErrorCodes.ExamGradeReleaseIdempotencyConflict,
            "شناسه عملیات انتشار قبلاً با درخواست دیگری استفاده شده است.");

    private static AssessmentException ConcurrencyConflict() =>
        new(
            AssessmentErrorCodes.ConcurrencyConflict,
            "نمره آزمون هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
}
