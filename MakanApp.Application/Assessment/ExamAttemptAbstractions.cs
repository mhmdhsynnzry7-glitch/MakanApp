using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public interface IExamAttemptService
{
    Task<StudentExamAttemptDto> StartAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        StartExamCommand command,
        CancellationToken cancellationToken);

    Task<StudentExamAttemptDto> GetMyActiveAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken);

    Task<StudentExamAttemptDto> GetAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<StudentExamAttemptSummaryDto>> GetMyAttemptsAsync(
        Guid userId,
        Guid sessionId,
        Guid examId,
        CancellationToken cancellationToken);
}

public interface IExamAttemptStore
{
    Task<IAssessmentTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken);

    Task<ExamStartContextRecord?> GetStartContextForUpdateAsync(
        Guid organizationId,
        Guid examId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ExamAttempt>> GetAttemptsForUpdateAsync(
        Guid organizationId,
        Guid examId,
        Guid enrollmentId,
        CancellationToken cancellationToken);

    Task<ExamAttempt?> GetByClientOperationForUpdateAsync(
        Guid organizationId,
        Guid enrollmentId,
        Guid clientOperationId,
        CancellationToken cancellationToken);

    Task<ExamAttemptRecord?> GetByIdAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<ExamAttemptRecord?> GetOwnedByIdAsync(
        Guid organizationId,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<ExamAttemptRecord?> GetOwnedActiveAsync(
        Guid organizationId,
        Guid examId,
        Guid userId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ExamAttemptRecord>> GetOwnedAttemptsAsync(
        Guid organizationId,
        Guid examId,
        Guid userId,
        CancellationToken cancellationToken);

    void Add(ExamAttempt attempt);
    void AddRange(IEnumerable<ExamAttemptQuestion> questions);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IExamQuestionOrderRandomizer
{
    IReadOnlyList<QuestionVersion> Randomize(IReadOnlyCollection<QuestionVersion> questions);
}
