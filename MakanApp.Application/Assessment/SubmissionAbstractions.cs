using MakanApp.Domain.Assessment;

namespace MakanApp.Application.Assessment;

public interface ISubmissionService
{
    Task<SubmissionAttemptResult> CreateOrResumeDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<SubmissionAttemptResult> SaveDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        SaveSubmissionDraftCommand command,
        CancellationToken cancellationToken);

    Task<SubmissionAttemptResult> AttachFileAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        AttachSubmissionFileCommand command,
        CancellationToken cancellationToken);

    Task<SubmissionAttemptResult> RemoveFileAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        Guid fileAssetId,
        string expectedRowVersion,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionAttemptResult>> GetMyAttemptsAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<SubmissionAttemptResult> GetAttemptAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SubmissionAttemptResult>> GetSubmittedAttemptsAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<SubmissionReceipt> FinalSubmitAsync(
        Guid userId,
        Guid sessionId,
        Guid attemptId,
        FinalSubmitAssignmentCommand command,
        CancellationToken cancellationToken);
}

public interface ISubmissionStore
{
    Task<IAssessmentTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken);
    Task<SubmissionEligibilityRecord?> GetEligibilityForStudentForUpdateAsync(
        Guid organizationId,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken);
    Task<SubmissionEligibilityRecord?> GetEligibilityForStudentAsync(
        Guid organizationId,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken);
    Task<Assignment?> GetAssignmentAsync(
        Guid organizationId,
        Guid assignmentId,
        CancellationToken cancellationToken);
    Task<SubmissionAttemptRecord?> GetAttemptForUpdateAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken);
    Task<SubmissionAttemptRecord?> GetAttemptAsync(
        Guid organizationId,
        Guid attemptId,
        CancellationToken cancellationToken);
    Task<SubmissionAttempt?> GetDraftForUpdateAsync(
        Guid organizationId,
        Guid assignmentRecipientId,
        Guid assignmentVersionId,
        CancellationToken cancellationToken);
    Task<int> CountSubmittedAttemptsForUpdateAsync(
        Guid organizationId,
        Guid assignmentRecipientId,
        Guid assignmentVersionId,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SubmissionAttemptRecord>> GetAttemptsForStudentAsync(
        Guid organizationId,
        Guid assignmentId,
        Guid studentUserId,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SubmissionAttemptRecord>> GetSubmittedAttemptsAsync(
        Guid organizationId,
        Guid assignmentId,
        CancellationToken cancellationToken);
    Task<bool> HasActiveTeacherAssignmentAsync(
        Guid organizationId,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken);
    void Add(SubmissionAttempt attempt);
    void Add(SubmissionAttachment attachment);
    void Remove(SubmissionAttachment attachment);
    void SetOriginalRowVersion(SubmissionAttempt attempt, byte[] rowVersion);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
