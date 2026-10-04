using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Application.Assessment;

public interface IAssignmentService
{
    Task<AssignmentResult> CreateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CreateAssignmentDraftCommand command,
        CancellationToken cancellationToken);

    Task<AssignmentResult> UpdateDraftAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        UpdateAssignmentDraftCommand command,
        CancellationToken cancellationToken);

    Task<PublishAssignmentResult> PublishAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        PublishAssignmentCommand command,
        CancellationToken cancellationToken);

    Task<AssignmentResult> GetAsync(
        Guid userId,
        Guid sessionId,
        Guid assignmentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AssignmentResult>> GetForClassAsync(
        Guid userId,
        Guid sessionId,
        Guid classId,
        CancellationToken cancellationToken);
}

public interface IAssessmentTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IAssignmentStore
{
    Task<IAssessmentTransaction> BeginSerializableTransactionAsync(CancellationToken cancellationToken);
    Task<AcademicClass?> GetClassAsync(Guid organizationId, Guid classId, CancellationToken cancellationToken);
    Task<AcademicClass?> GetClassForUpdateAsync(Guid organizationId, Guid classId, CancellationToken cancellationToken);
    Task<bool> HasActiveTeacherAssignmentAsync(Guid organizationId, Guid classId, Guid membershipId, CancellationToken cancellationToken);
    Task<AssignmentWithVersionRecord?> GetAssignmentAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken);
    Task<AssignmentWithVersionRecord?> GetAssignmentForUpdateAsync(Guid organizationId, Guid assignmentId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> GetActiveEnrollmentIdsForUpdateAsync(Guid organizationId, Guid classId, CancellationToken cancellationToken);
    Task<bool> IsRecipientForStudentAsync(Guid organizationId, Guid assignmentVersionId, Guid userId, CancellationToken cancellationToken);
    Task<bool> IsRecipientForLearnerAsync(Guid organizationId, Guid assignmentVersionId, Guid learnerOrganizationPersonId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForManagerAsync(Guid organizationId, Guid classId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForTeacherAsync(Guid organizationId, Guid classId, Guid membershipId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForStudentAsync(Guid organizationId, Guid classId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AssignmentWithVersionRecord>> GetForLearnerAsync(Guid organizationId, Guid classId, Guid learnerOrganizationPersonId, CancellationToken cancellationToken);
    void SetOriginalRowVersion(Assignment assignment, byte[] rowVersion);
    void SetOriginalRowVersion(AssignmentVersion version, byte[] rowVersion);
    void Add(Assignment assignment);
    void Add(AssignmentVersion version);
    void AddRange(IEnumerable<AssignmentRecipient> recipients);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
