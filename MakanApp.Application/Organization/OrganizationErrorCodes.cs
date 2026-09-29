namespace MakanApp.Application.Organization;

public static class OrganizationErrorCodes
{
    public const string OrganizationNotFound = "ORGANIZATION_NOT_FOUND";
    public const string MembershipNotActive = "MEMBERSHIP_NOT_ACTIVE";
    public const string RoleNotActive = "ROLE_NOT_ACTIVE";
    public const string InvitationNotFound = "INVITATION_NOT_FOUND";
    public const string InvitationExpired = "INVITATION_EXPIRED";
    public const string InvitationRevoked = "INVITATION_REVOKED";
    public const string InvitationAlreadyAccepted = "INVITATION_ALREADY_ACCEPTED";
    public const string WorkspaceNotAllowed = "WORKSPACE_NOT_ALLOWED";
    public const string WorkspaceNotFound = "WORKSPACE_NOT_FOUND";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
}
