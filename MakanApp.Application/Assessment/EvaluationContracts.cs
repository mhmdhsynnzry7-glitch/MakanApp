using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public enum EvaluationReviewStatus
{
    AwaitingEvaluation = 1,
    Draft = 2,
    Released = 3,
    CorrectionDraft = 4
}

public sealed record EvaluationQueueQuery(
    Guid? AssignmentId,
    Guid? ClassId,
    EvaluationReviewStatus? ReviewStatus,
    bool? IsLate);

public sealed record EvaluationQueueItemResult(
    Guid SubmissionAttemptId,
    Guid AssignmentId,
    Guid AssignmentVersionId,
    Guid ClassId,
    string AssignmentTitle,
    string StudentDisplayName,
    int AttemptNumber,
    DateTime SubmittedAtUtc,
    bool IsLate,
    decimal MaxScore,
    EvaluationReviewStatus ReviewStatus,
    int? EvaluationRevisionNumber,
    decimal? Score);

public sealed record SubmissionForEvaluationResult(
    Guid SubmissionAttemptId,
    Guid AssignmentId,
    Guid AssignmentVersionId,
    Guid ClassId,
    string AssignmentTitle,
    string StudentDisplayName,
    int AttemptNumber,
    DateTime SubmittedAtUtc,
    bool IsLate,
    decimal MaxScore,
    string? AnswerText,
    IReadOnlyCollection<SubmissionAttachmentResult> Attachments);

public sealed record SaveEvaluationDraftCommand(
    decimal Score,
    string? LearnerFeedback,
    string? GuardianVisibleFeedback,
    string? TeacherPrivateNote,
    string? ExpectedRowVersion);

public sealed record ReleaseEvaluationCommand(string ExpectedRowVersion);

public sealed record CorrectReleasedEvaluationCommand(
    decimal Score,
    string? LearnerFeedback,
    string? GuardianVisibleFeedback,
    string? TeacherPrivateNote,
    string CorrectionReason,
    string ExpectedReleasedEvaluationRowVersion);

public sealed record EvaluatorEvaluationResult(
    Guid Id,
    Guid SubmissionAttemptId,
    int RevisionNumber,
    decimal Score,
    decimal MaxScore,
    string? LearnerFeedback,
    string? GuardianVisibleFeedback,
    string? TeacherPrivateNote,
    EvaluationRevisionStatus Status,
    Guid CreatedByMembershipId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid? SupersedesEvaluationRevisionId,
    string? CorrectionReason,
    DateTime? ReleasedAtUtc,
    string RowVersion);

public sealed record GradeReleaseResult(
    Guid GradeReleaseId,
    Guid SubmissionAttemptId,
    Guid EvaluationRevisionId,
    int RevisionNumber,
    decimal Score,
    DateTime ReleasedAtUtc,
    Guid ReleasedByMembershipId,
    string EvaluationRowVersion);

public sealed record StudentReleasedResult(
    Guid SubmissionAttemptId,
    Guid AssignmentId,
    Guid AssignmentVersionId,
    int EvaluationRevisionNumber,
    decimal Score,
    decimal MaxScore,
    string? LearnerFeedback,
    DateTime ReleasedAtUtc);

public sealed record ParentReleasedResult(
    Guid SubmissionAttemptId,
    Guid AssignmentId,
    Guid AssignmentVersionId,
    int EvaluationRevisionNumber,
    decimal Score,
    decimal MaxScore,
    string? GuardianVisibleFeedback,
    DateTime ReleasedAtUtc);

public sealed record EvaluationSubmissionRecord(
    SubmissionAttempt Attempt,
    Assignment Assignment,
    AssignmentVersion Version,
    MakanApp.Domain.Academic.Enrollment Enrollment,
    Guid StudentUserId,
    string StudentDisplayName,
    IReadOnlyCollection<SubmissionAttachmentWithFileRecord> Attachments);

public sealed record EvaluationAggregateRecord(
    EvaluationSubmissionRecord Submission,
    EvaluationRevision? LatestEvaluation,
    EvaluationRevision? CurrentReleasedEvaluation,
    GradeRelease? CurrentGradeRelease);

public sealed record EvaluationQueueRecord(
    EvaluationSubmissionRecord Submission,
    EvaluationRevision? LatestEvaluation,
    EvaluationRevision? CurrentReleasedEvaluation);
