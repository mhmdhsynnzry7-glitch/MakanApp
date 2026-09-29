using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public interface IOrganizationService
{
    Task<IReadOnlyCollection<WorkspaceResult>> GetMyWorkspacesAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<AccessContext> SelectWorkspaceAsync(
        Guid userId,
        Guid sessionId,
        SelectWorkspaceCommand command,
        CancellationToken cancellationToken);

    Task<AccessContext> GetCurrentAccessContextAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<InvitationResult>> GetMyInvitationsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<AcceptInvitationResult> AcceptInvitationAsync(
        Guid userId,
        Guid invitationId,
        CancellationToken cancellationToken);

    Task<DeclineInvitationResult> DeclineInvitationAsync(
        Guid userId,
        Guid invitationId,
        CancellationToken cancellationToken);
}

public interface IAccessContextResolver
{
    Task<AccessContext> ResolveAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);
}

public interface IOrganizationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}

public interface IOrganizationStore
{
    Task<IOrganizationTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<OrganizationWorkspaceRecord>> GetWorkspaceRecordsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<OrganizationWorkspaceRecord?> GetActiveWorkspaceRecordAsync(
        Guid userId,
        Guid membershipId,
        OrganizationRole role,
        CancellationToken cancellationToken);

    Task<Membership?> GetMembershipAsync(
        Guid membershipId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<Membership?> GetActiveMembershipAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<RoleAssignment?> GetActiveRoleAssignmentAsync(
        Guid membershipId,
        OrganizationRole role,
        CancellationToken cancellationToken);

    Task<MakanApp.Domain.Organization.Organization?> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<InvitationWithOrganization>> GetInvitationsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Invitation?> GetInvitationForUpdateAsync(
        Guid invitationId,
        Guid destinationUserId,
        CancellationToken cancellationToken);

    Task<UserSession?> GetSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken);

    void Add(Membership membership);
    void Add(RoleAssignment roleAssignment);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
