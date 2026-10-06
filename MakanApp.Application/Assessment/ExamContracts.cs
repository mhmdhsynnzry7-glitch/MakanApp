using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public sealed record CreateExamDraftCommand(
    string Title,
    string? Description,
    DateTime AvailableFromUtc,
    DateTime AvailableUntilUtc,
    int DurationMinutes,
    int MaxAttempts,
    decimal MaxScore,
    ExamRandomizationPolicy RandomizationPolicy);

public sealed record UpdateExamDraftCommand(
    string Title,
    string? Description,
    DateTime AvailableFromUtc,
    DateTime AvailableUntilUtc,
    int DurationMinutes,
    int MaxAttempts,
    decimal MaxScore,
    ExamRandomizationPolicy RandomizationPolicy,
    string ExpectedVersionRowVersion);

public sealed record ExamQuestionOptionCommand(int Order, string Text, bool IsCorrect);

public sealed record AddExamQuestionCommand(
    int Order,
    ExamQuestionType Type,
    string Prompt,
    decimal Score,
    IReadOnlyCollection<ExamQuestionOptionCommand>? Options,
    string ExpectedVersionRowVersion);

public sealed record UpdateExamQuestionCommand(
    int Order,
    ExamQuestionType Type,
    string Prompt,
    decimal Score,
    IReadOnlyCollection<ExamQuestionOptionCommand>? Options,
    string ExpectedVersionRowVersion,
    string ExpectedQuestionRowVersion);

public sealed record DeleteExamQuestionCommand(
    string ExpectedVersionRowVersion,
    string ExpectedQuestionRowVersion);

public sealed record PublishExamCommand(
    string ExpectedExamRowVersion,
    string ExpectedVersionRowVersion);

public sealed record CreateNextExamVersionCommand(
    string ExpectedExamRowVersion,
    string ExpectedPublishedVersionRowVersion);

public sealed record ExamEditorOptionDto(
    Guid Id,
    int Order,
    string Text,
    bool IsCorrect);

public sealed record ExamEditorQuestionDto(
    Guid Id,
    int Order,
    ExamQuestionType Type,
    string Prompt,
    decimal Score,
    IReadOnlyCollection<ExamEditorOptionDto> Options,
    string RowVersion);

public sealed record ExamEditorDto(
    Guid Id,
    Guid OrganizationId,
    Guid ClassId,
    string ClassTitle,
    ExamStatus Status,
    DateTime CreatedAtUtc,
    string ExamRowVersion,
    Guid VersionId,
    int VersionNumber,
    ExamVersionStatus VersionStatus,
    string Title,
    string? Description,
    DateTime AvailableFromUtc,
    DateTime AvailableUntilUtc,
    int DurationMinutes,
    int MaxAttempts,
    decimal MaxScore,
    ExamRandomizationPolicy RandomizationPolicy,
    DateTime? PublishedAtUtc,
    string VersionRowVersion,
    IReadOnlyCollection<ExamEditorQuestionDto> Questions);

public sealed record StudentExamRules(
    DateTime AvailableFromUtc,
    DateTime AvailableUntilUtc,
    int DurationMinutes,
    int MaxAttempts,
    decimal MaxScore,
    ExamRandomizationPolicy RandomizationPolicy);

public sealed record StudentExamSummary(
    Guid Id,
    Guid VersionId,
    Guid ClassId,
    string ClassTitle,
    int VersionNumber,
    string Title,
    string? Description,
    ExamVersionStatus PublicationState,
    StudentExamRules Rules);

public sealed record StudentSafeExamOption(
    Guid Id,
    int Order,
    string Text);

public sealed record StudentSafeExamQuestion(
    Guid Id,
    int Order,
    ExamQuestionType Type,
    string Prompt,
    decimal Score,
    IReadOnlyCollection<StudentSafeExamOption> Options);

public sealed record StudentSafeExamPreview(
    StudentExamSummary Exam,
    IReadOnlyCollection<StudentSafeExamQuestion> Questions);

public sealed record ExamTeacherPreview(
    StudentExamSummary Exam,
    IReadOnlyCollection<ExamEditorQuestionDto> Questions);

public sealed record ExamAggregateRecord(
    Exam Exam,
    ExamVersion Version,
    string ClassTitle,
    bool ClassIsActive,
    IReadOnlyCollection<QuestionVersion> Questions);

public sealed record ExamSummaryRecord(
    Exam Exam,
    ExamVersion Version,
    string ClassTitle);
