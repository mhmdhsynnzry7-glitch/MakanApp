using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed record StartExamCommand(Guid ClientOperationId);

public sealed record StudentExamAttemptOptionDto(
    Guid Id,
    int Order,
    string Text);

public sealed record StudentExamAttemptQuestionDto(
    Guid AttemptQuestionId,
    Guid QuestionVersionId,
    int DisplayOrder,
    ExamQuestionType QuestionType,
    string Prompt,
    decimal Score,
    IReadOnlyCollection<StudentExamAttemptOptionDto> Options);

public sealed record StudentExamAttemptDto(
    Guid ExamAttemptId,
    Guid ExamId,
    Guid ExamVersionId,
    int AttemptNumber,
    DateTime StartedAtUtc,
    DateTime EffectiveDeadlineUtc,
    DateTime ServerNowUtc,
    ExamAttemptStatus Status,
    IReadOnlyCollection<StudentExamAttemptQuestionDto> Questions);

public sealed record StudentExamAttemptSummaryDto(
    Guid ExamAttemptId,
    Guid ExamVersionId,
    int AttemptNumber,
    DateTime StartedAtUtc,
    DateTime EffectiveDeadlineUtc,
    DateTime ServerNowUtc,
    ExamAttemptStatus Status);

public sealed record ExamStartContextRecord(
    Exam Exam,
    MakanApp.Domain.Academic.Enrollment? Enrollment,
    ExamVersion? Version,
    IReadOnlyCollection<QuestionVersion> Questions);

public sealed record FrozenExamQuestionRecord(
    ExamAttemptQuestion AttemptQuestion,
    QuestionVersion Question);

public sealed record ExamAttemptRecord(
    ExamAttempt Attempt,
    IReadOnlyCollection<FrozenExamQuestionRecord> Questions);
