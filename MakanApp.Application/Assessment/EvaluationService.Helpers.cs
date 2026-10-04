using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Assessment;

public sealed partial class EvaluationService
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
            throw EvaluationNotAllowed();
        }

        return context;
    }

    private async Task EnsureCanEvaluateAsync(
        AccessContext context,
        EvaluationSubmissionRecord submission,
        CancellationToken cancellationToken)
    {
        if (context.ActiveRole == OrganizationRole.Manager)
        {
            return;
        }

        if (context.ActiveRole == OrganizationRole.Teacher)
        {
            if (await _store.HasActiveTeacherAssignmentAsync(
                submission.Attempt.OrganizationId,
                submission.Assignment.ClassId,
                context.MembershipId!.Value,
                cancellationToken))
            {
                return;
            }

            throw new AssessmentException(
                AssessmentErrorCodes.TeacherNotAssigned,
                "معلم برای کلاس این پاسخ TeacherAssignment فعال ندارد.");
        }

        throw EvaluationNotAllowed();
    }

    private static void EnsureSubmitted(SubmissionAttempt attempt)
    {
        if (attempt.Status != SubmissionAttemptStatus.Submitted || !attempt.SubmittedAtUtc.HasValue)
        {
            throw new AssessmentException(
                AssessmentErrorCodes.EvaluationSubmissionNotFinal,
                "فقط تلاش نهایی‌شده قابل ارزیابی است.");
        }
    }

    private async Task<EvaluationAggregateRecord> GetAggregateAsync(
        AccessContext context,
        Guid attemptId,
        CancellationToken cancellationToken) =>
        await _store.GetAggregateAsync(
            context.OrganizationId!.Value,
            attemptId,
            cancellationToken) ?? throw EvaluationNotFound();

    private async Task<EvaluationAggregateRecord> GetAggregateForUpdateAsync(
        AccessContext context,
        Guid attemptId,
        CancellationToken cancellationToken) =>
        await _store.GetAggregateForUpdateAsync(
            context.OrganizationId!.Value,
            attemptId,
            cancellationToken) ?? throw EvaluationNotFound();

    private void ApplyExpectedRowVersion(EvaluationRevision evaluation, string? encodedRowVersion)
    {
        if (string.IsNullOrWhiteSpace(encodedRowVersion))
        {
            throw ConcurrencyConflict();
        }

        var rowVersion = DecodeRowVersion(encodedRowVersion);
        if (!evaluation.RowVersion.SequenceEqual(rowVersion))
        {
            throw ConcurrencyConflict();
        }

        _store.SetOriginalRowVersion(evaluation, rowVersion);
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
            ArgumentOutOfRangeException { ParamName: "score" } => new AssessmentException(
                AssessmentErrorCodes.EvaluationScoreInvalid,
                exception.Message),
            ArgumentException { ParamName: "correctionReason" } => new AssessmentException(
                AssessmentErrorCodes.EvaluationCorrectionReasonRequired,
                exception.Message),
            InvalidOperationException => EvaluationAlreadyReleased(),
            _ => new AssessmentException(
                AssessmentErrorCodes.EvaluationNotReadyForRelease,
                exception.Message)
        };

    private static EvaluationQueueItemResult ToQueueResult(EvaluationQueueRecord record)
    {
        var reviewStatus = record.LatestEvaluation switch
        {
            null => EvaluationReviewStatus.AwaitingEvaluation,
            { IsDraft: true, SupersedesEvaluationRevisionId: not null } => EvaluationReviewStatus.CorrectionDraft,
            { IsDraft: true } => EvaluationReviewStatus.Draft,
            _ => EvaluationReviewStatus.Released
        };
        return new EvaluationQueueItemResult(
            record.Submission.Attempt.Id,
            record.Submission.Assignment.Id,
            record.Submission.Version.Id,
            record.Submission.Assignment.ClassId,
            record.Submission.Version.Title,
            record.Submission.StudentDisplayName,
            record.Submission.Attempt.AttemptNumber,
            record.Submission.Attempt.SubmittedAtUtc!.Value,
            record.Submission.Attempt.IsLate,
            record.Submission.Version.MaxScore,
            reviewStatus,
            record.LatestEvaluation?.RevisionNumber,
            record.LatestEvaluation?.Score);
    }

    private static SubmissionForEvaluationResult ToSubmissionResult(EvaluationSubmissionRecord record) =>
        new(
            record.Attempt.Id,
            record.Assignment.Id,
            record.Version.Id,
            record.Assignment.ClassId,
            record.Version.Title,
            record.StudentDisplayName,
            record.Attempt.AttemptNumber,
            record.Attempt.SubmittedAtUtc!.Value,
            record.Attempt.IsLate,
            record.Version.MaxScore,
            record.Attempt.AnswerText,
            record.Attachments.Select(attachment => new SubmissionAttachmentResult(
                attachment.FileAsset.Id,
                attachment.FileAsset.OriginalFileName,
                attachment.FileAsset.ContentType,
                attachment.FileAsset.SizeBytes,
                attachment.FileAsset.Status,
                attachment.Attachment.AttachedAtUtc)).ToArray());

    private static EvaluatorEvaluationResult ToEvaluatorResult(
        EvaluationRevision evaluation,
        decimal maxScore,
        GradeRelease? gradeRelease) =>
        new(
            evaluation.Id,
            evaluation.SubmissionAttemptId,
            evaluation.RevisionNumber,
            evaluation.Score,
            maxScore,
            evaluation.LearnerFeedback,
            evaluation.GuardianVisibleFeedback,
            evaluation.TeacherPrivateNote,
            evaluation.Status,
            evaluation.CreatedByMembershipId,
            evaluation.CreatedAtUtc,
            evaluation.UpdatedAtUtc,
            evaluation.SupersedesEvaluationRevisionId,
            evaluation.CorrectionReason,
            gradeRelease?.ReleasedAtUtc,
            Convert.ToBase64String(evaluation.RowVersion));

    private static GradeReleaseResult ToReleaseResult(
        GradeRelease gradeRelease,
        EvaluationRevision evaluation) =>
        new(
            gradeRelease.Id,
            gradeRelease.SubmissionAttemptId,
            evaluation.Id,
            evaluation.RevisionNumber,
            evaluation.Score,
            gradeRelease.ReleasedAtUtc,
            gradeRelease.ReleasedByMembershipId,
            Convert.ToBase64String(evaluation.RowVersion));

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static AssessmentException EvaluationNotFound() =>
        new(AssessmentErrorCodes.EvaluationNotFound, "ارزیابی پیدا نشد.");

    private static AssessmentException EvaluationNotAllowed() =>
        new(AssessmentErrorCodes.EvaluationNotAllowed, "دسترسی به ارزیابی مجاز نیست.");

    private static AssessmentException EvaluationAlreadyReleased() =>
        new(AssessmentErrorCodes.EvaluationAlreadyReleased, "ارزیابی منتشرشده قابل ویرایش مستقیم نیست.");

    private static AssessmentException GradeNotReleased() =>
        new(AssessmentErrorCodes.GradeNotReleased, "نتیجه منتشرشده پیدا نشد.");

    private static AssessmentException GradeReleaseConflict() =>
        new(AssessmentErrorCodes.GradeReleaseConflict, "وضعیت انتشار نمره هم‌زمان تغییر کرده است.");

    private static AssessmentException ConcurrencyConflict() =>
        new(AssessmentErrorCodes.ConcurrencyConflict, "ارزیابی هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
}
