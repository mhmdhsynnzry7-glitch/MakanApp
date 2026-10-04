using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public interface IEvaluationService
{
    Task<IReadOnlyCollection<EvaluationQueueItemResult>> GetQueueAsync(
        Guid userId,
        Guid sessionId,
        EvaluationQueueQuery query,
        CancellationToken cancellationToken);

    Task<SubmissionForEvaluationResult> GetSubmissionForEvaluationAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<EvaluatorEvaluationResult> CreateOrUpdateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        SaveEvaluationDraftCommand command,
        CancellationToken cancellationToken);

    Task<EvaluatorEvaluationResult> GetEvaluationAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<GradeReleaseResult> ReleaseAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        ReleaseEvaluationCommand command,
        CancellationToken cancellationToken);

    Task<EvaluatorEvaluationResult> CorrectReleasedEvaluationAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CorrectReleasedEvaluationCommand command,
        CancellationToken cancellationToken);

    Task<StudentReleasedResult> GetReleasedAssignmentResultAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<ParentReleasedResult> GetGuardianReleasedAssignmentResultAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);
}

public interface IEvaluationStore
{
    Task<IAssessmentTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken);
    Task<EvaluationAggregateRecord?> GetAggregateAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken);
    Task<EvaluationAggregateRecord?> GetAggregateForUpdateAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EvaluationQueueRecord>> GetQueueForManagerAsync(
        Guid organizationId,
        EvaluationQueueQuery query,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<EvaluationQueueRecord>> GetQueueForTeacherAsync(
        Guid organizationId,
        Guid membershipId,
        EvaluationQueueQuery query,
        CancellationToken cancellationToken);
    Task<bool> HasActiveTeacherAssignmentAsync(
        Guid organizationId,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken);
    void Add(EvaluationRevision evaluationRevision);
    void Add(GradeRelease gradeRelease);
    void SetOriginalRowVersion(EvaluationRevision evaluationRevision, byte[] rowVersion);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
