using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public enum ExamGradingQueueStatus
{
    AwaitingGrading = 1,
    Draft = 2,
    ReadyForRelease = 3,
    Released = 4,
    CorrectionDraft = 5
}

public enum ExamResultReleaseStatus
{
    AwaitingGrading = 1,
    AwaitingRelease = 2,
    Released = 3
}

public sealed record ExamGradingQueueQuery(
    Guid? ClassId,
    ExamGradingQueueStatus? Status);

public sealed record ExamGradingQueueItemDto(
    Guid ExamAttemptId,
    Guid ExamId,
    Guid ExamVersionId,
    Guid ClassId,
    string ExamTitle,
    string StudentDisplayName,
    int AttemptNumber,
    DateTime FinalizedAtUtc,
    decimal MaximumScore,
    ExamGradingQueueStatus Status,
    int? GradeRevisionNumber,
    bool RequiresManualReview);

public sealed record GradeExamQuestionCommand(
    decimal AwardedScore,
    string? LearnerFeedback,
    string? EvaluatorPrivateNote,
    string ExpectedGradeRowVersion);

public sealed record CompleteExamGradeCommand(
    string? LearnerFeedback,
    string? GuardianVisibleFeedback,
    string? EvaluatorPrivateNote,
    string ExpectedGradeRowVersion);

public sealed record ReleaseExamGradeCommand(
    Guid ClientOperationId,
    string ExpectedGradeRowVersion);

public sealed record CorrectReleasedExamGradeCommand(
    string CorrectionReason,
    string ExpectedReleasedGradeRowVersion);

public sealed record ExamQuestionGradeDto(
    Guid ExamAttemptQuestionId,
    Guid QuestionVersionId,
    int DisplayOrder,
    ExamQuestionType QuestionType,
    string Prompt,
    bool IsAnswered,
    string? SelectedOptionText,
    string? TextAnswer,
    decimal AwardedScore,
    decimal MaximumScore,
    ExamQuestionGradingMode GradingMode,
    bool IsReviewed,
    string? LearnerFeedback,
    string? EvaluatorPrivateNote,
    Guid? ReviewedByMembershipId,
    DateTime? ReviewedAtUtc);

public sealed record ExamGradeDto(
    Guid ExamGradeRevisionId,
    Guid ExamAttemptId,
    Guid ExamId,
    Guid ExamVersionId,
    Guid ClassId,
    string ExamTitle,
    string StudentDisplayName,
    int AttemptNumber,
    DateTime FinalizedAtUtc,
    int RevisionNumber,
    decimal TotalScore,
    decimal MaximumScore,
    ExamGradeRevisionStatus Status,
    string? LearnerFeedback,
    string? GuardianVisibleFeedback,
    string? EvaluatorPrivateNote,
    Guid CreatedByMembershipId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid? SupersedesExamGradeRevisionId,
    string? CorrectionReason,
    DateTime? ReleasedAtUtc,
    string RowVersion,
    IReadOnlyCollection<ExamQuestionGradeDto> Questions);

public sealed record ExamGradeReleaseDto(
    Guid ExamGradeReleaseId,
    Guid ExamAttemptId,
    Guid ExamGradeRevisionId,
    int GradeRevisionNumber,
    decimal TotalScore,
    decimal MaximumScore,
    DateTime ReleasedAtUtc,
    Guid ReleasedByMembershipId,
    string GradeRowVersion);

public sealed record StudentExamResultDto(
    Guid ExamAttemptId,
    Guid ExamId,
    Guid ExamVersionId,
    int AttemptNumber,
    DateTime FinalizedAtUtc,
    ExamResultReleaseStatus ReleaseStatus,
    int? GradeRevisionNumber,
    DateTime? ReleasedAtUtc,
    decimal? Score,
    decimal? MaximumScore,
    string? LearnerFeedback);

public sealed record GuardianExamResultDto(
    Guid ExamAttemptId,
    Guid ExamId,
    Guid ExamVersionId,
    int AttemptNumber,
    DateTime FinalizedAtUtc,
    ExamResultReleaseStatus ReleaseStatus,
    int? GradeRevisionNumber,
    DateTime? ReleasedAtUtc,
    decimal? Score,
    decimal? MaximumScore,
    string? GuardianVisibleFeedback);

public sealed record ExamGradingQuestionSourceRecord(
    ExamAttemptQuestion AttemptQuestion,
    QuestionVersion Question,
    ExamFinalAnswer? FinalAnswer,
    AnswerRevision? FinalRevision);

public sealed record ExamGradingSourceRecord(
    ExamAttempt Attempt,
    Exam Exam,
    ExamVersion Version,
    Enrollment Enrollment,
    Guid StudentUserId,
    string StudentDisplayName,
    IReadOnlyCollection<ExamGradingQuestionSourceRecord> Questions);

public sealed record ExamGradeAggregateRecord(
    ExamGradingSourceRecord Source,
    IReadOnlyCollection<ExamGradeRevision> Revisions,
    ExamGradeRevision? LatestRevision,
    ExamGradeRevision? CurrentReleasedRevision,
    ExamGradeRelease? CurrentRelease,
    IReadOnlyCollection<ExamQuestionGrade> LatestQuestionGrades,
    IReadOnlyCollection<ExamGradeRelease> Releases);

public sealed record ExamGradingQueueRecord(
    ExamGradingSourceRecord Source,
    ExamGradeRevision? LatestRevision,
    ExamGradeRevision? CurrentReleasedRevision);
