using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public interface IExamAnswerService
{
    Task<ExamWriteLeaseDto> AcquireWriteLeaseAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<ExamWriteLeaseDto> TransferWriteLeaseAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<ExamAnswerReceiptDto> SaveAnswerAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        Guid attemptQuestionId,
        SaveExamAnswerCommand command,
        CancellationToken cancellationToken);

    Task<StudentExamAnswersDto> GetMyAnswersAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);
}

public interface IExamAnswerStore
{
    Task<IAssessmentTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken);

    Task<ExamAttempt?> GetOwnedAttemptForUpdateAsync(
        Guid organizationId,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<ExamAnswersRecord?> GetOwnedAnswersAsync(
        Guid organizationId,
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<ExamAnswerQuestionRecord?> GetQuestionForUpdateAsync(
        Guid organizationId,
        Guid attemptId,
        Guid attemptQuestionId,
        CancellationToken cancellationToken);

    Task<AnswerRevision?> GetByClientOperationForUpdateAsync(
        Guid organizationId,
        Guid attemptId,
        Guid clientOperationId,
        CancellationToken cancellationToken);

    void Add(AnswerRevision revision);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
