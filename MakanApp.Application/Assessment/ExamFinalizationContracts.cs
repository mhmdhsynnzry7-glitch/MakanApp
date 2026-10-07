using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed record FinalizeExamCommand(
    Guid ClientOperationId,
    long ExpectedAnswerSetVersion,
    long WriteLeaseVersion);

public sealed record ExamFinalReceiptDto(
    Guid ExamAttemptId,
    Guid ExamId,
    Guid ExamVersionId,
    int AttemptNumber,
    DateTime StartedAtUtc,
    DateTime FinalizedAtUtc,
    long FinalizedAnswerSetVersion,
    int AnsweredQuestionCount,
    int TotalQuestionCount,
    ExamAttemptStatus Status);

public sealed record ExamFinalizationQuestionRecord(
    ExamAttemptQuestion AttemptQuestion,
    AnswerRevision? CurrentRevision);

public sealed record ExamFinalAnswerRecord(
    ExamFinalAnswer FinalAnswer,
    AnswerRevision Revision);

public sealed record ExamFinalizationRecord(
    ExamAttempt Attempt,
    IReadOnlyCollection<ExamAttemptQuestion> Questions,
    IReadOnlyCollection<ExamFinalAnswerRecord> FinalAnswers);
