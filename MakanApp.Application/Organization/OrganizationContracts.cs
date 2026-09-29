using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public sealed record WorkspaceResult(
    WorkspaceType WorkspaceType,
    Guid? OrganizationId,
    string? OrganizationName,
    Guid? MembershipId,
    OrganizationRole? Role,
    string DisplayName,
    string Status);

public sealed record SelectWorkspaceCommand(
    WorkspaceType WorkspaceType,
    Guid? MembershipId,
    OrganizationRole? Role);

public sealed record AccessContext(
    Guid ActorId,
    Guid UserId,
    Guid SessionId,
    WorkspaceType WorkspaceType,
    Guid? OrganizationId,
    Guid? MembershipId,
    OrganizationRole? ActiveRole);

public sealed record InvitationResult(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    Guid InvitedByUserId,
    OrganizationRole Role,
    InvitationStatus Status,
    DateTime ExpiresAtUtc);

public sealed record AcceptInvitationResult(
    Guid InvitationId,
    Guid OrganizationId,
    string OrganizationName,
    Guid MembershipId,
    Guid RoleAssignmentId,
    OrganizationRole Role,
    bool AlreadyAccepted);

public sealed record DeclineInvitationResult(
    Guid InvitationId,
    InvitationStatus Status);

public sealed record OrganizationWorkspaceRecord(
    Guid OrganizationId,
    string OrganizationName,
    Guid MembershipId,
    OrganizationRole Role);

public sealed record InvitationWithOrganization(
    Invitation Invitation,
    string OrganizationName);
