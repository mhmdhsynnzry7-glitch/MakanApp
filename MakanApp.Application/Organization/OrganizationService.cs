using MakanApp.Application.Identity;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public sealed class OrganizationService(
    IOrganizationStore store,
    TimeProvider timeProvider) : IOrganizationService, IAccessContextResolver
{
    public async Task<IReadOnlyCollection<WorkspaceResult>> GetMyWorkspacesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var records = await store.GetWorkspaceRecordsAsync(userId, cancellationToken);
        var workspaces = new List<WorkspaceResult>(records.Count + 1)
        {
            WorkspaceFactory.CreatePersonal()
        };
        workspaces.AddRange(records.Select(WorkspaceFactory.CreateOrganization));
        return workspaces;
    }

    public async Task<AccessContext> SelectWorkspaceAsync(
        Guid userId,
        Guid sessionId,
        SelectWorkspaceCommand command,
        CancellationToken cancellationToken)
    {
        var session = await GetActiveSessionAsync(userId, sessionId, cancellationToken);
        if (command.WorkspaceType == WorkspaceType.Personal)
        {
            session.SelectPersonalWorkspace();
            await store.SaveChangesAsync(cancellationToken);
            return CreatePersonalContext(userId, sessionId);
        }

        if (command.WorkspaceType != WorkspaceType.Organization ||
            !command.MembershipId.HasValue ||
            !command.Role.HasValue)
        {
            throw WorkspaceNotFoundException();
        }

        var membership = await store.GetMembershipAsync(
            command.MembershipId.Value,
            userId,
            cancellationToken);
        if (membership is null)
        {
            throw WorkspaceNotFoundException();
        }

        if (!membership.IsActive)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.MembershipNotActive,
                "عضویت انتخاب‌شده فعال نیست.");
        }

        var organization = await store.GetOrganizationAsync(
            membership.OrganizationId,
            cancellationToken);
        if (organization is null || !organization.IsActive)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.OrganizationNotFound,
                "سازمان انتخاب‌شده در دسترس نیست.");
        }

        var roleAssignment = await store.GetActiveRoleAssignmentAsync(
            membership.Id,
            command.Role.Value,
            cancellationToken);
        if (roleAssignment is null)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.RoleNotActive,
                "نقش انتخاب‌شده فعال نیست.");
        }

        session.SelectOrganizationWorkspace(membership.Id, roleAssignment.Role.ToString());
        await store.SaveChangesAsync(cancellationToken);

        return new AccessContext(
            userId,
            userId,
            sessionId,
            WorkspaceType.Organization,
            organization.Id,
            membership.Id,
            roleAssignment.Role);
    }

    public Task<AccessContext> GetCurrentAccessContextAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        ResolveAsync(userId, sessionId, cancellationToken);

    public async Task<AccessContext> ResolveAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await GetActiveSessionAsync(userId, sessionId, cancellationToken);
        if (!session.SelectedMembershipId.HasValue &&
            string.IsNullOrWhiteSpace(session.SelectedRole))
        {
            return CreatePersonalContext(userId, sessionId);
        }

        if (!session.SelectedMembershipId.HasValue ||
            !Enum.TryParse<OrganizationRole>(session.SelectedRole, out var role))
        {
            throw WorkspaceNotAllowedException();
        }

        var workspace = await store.GetActiveWorkspaceRecordAsync(
            userId,
            session.SelectedMembershipId.Value,
            role,
            cancellationToken);
        if (workspace is null)
        {
            throw WorkspaceNotAllowedException();
        }

        return new AccessContext(
            userId,
            userId,
            sessionId,
            WorkspaceType.Organization,
            workspace.OrganizationId,
            workspace.MembershipId,
            workspace.Role);
    }

    public async Task<IReadOnlyCollection<InvitationResult>> GetMyInvitationsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var invitations = await store.GetInvitationsAsync(userId, cancellationToken);
        return invitations
            .Select(item => new InvitationResult(
                item.Invitation.Id,
                item.Invitation.OrganizationId,
                item.OrganizationName,
                item.Invitation.InvitedByUserId,
                item.Invitation.Role,
                item.Invitation.GetEffectiveStatus(nowUtc),
                item.Invitation.ExpiresAtUtc))
            .ToArray();
    }

    public async Task<AcceptInvitationResult> AcceptInvitationAsync(
        Guid userId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var invitation = await store.GetInvitationForUpdateAsync(
            invitationId,
            userId,
            cancellationToken);
        if (invitation is null)
        {
            throw InvitationNotFoundException();
        }

        var organization = await store.GetOrganizationAsync(
            invitation.OrganizationId,
            cancellationToken);
        if (organization is null || !organization.IsActive)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.OrganizationNotFound,
                "سازمان دعوت در دسترس نیست.");
        }

        if (invitation.Status == InvitationStatus.Accepted)
        {
            await transaction.CommitAsync(cancellationToken);
            return CreateAcceptanceResult(invitation, organization.Name, true);
        }

        if (invitation.Status == InvitationStatus.Revoked)
        {
            throw InvitationRevokedException();
        }

        if (invitation.Status == InvitationStatus.Declined)
        {
            throw InvitationNotFoundException();
        }

        if (invitation.GetEffectiveStatus(nowUtc) == InvitationStatus.Expired)
        {
            invitation.Accept(Guid.Empty, Guid.Empty, nowUtc);
            await store.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw InvitationExpiredException();
        }

        var membership = await store.GetActiveMembershipAsync(
            userId,
            invitation.OrganizationId,
            cancellationToken);
        var membershipWasCreated = membership is null;
        membership ??= Membership.CreateActive(userId, invitation.OrganizationId, nowUtc);
        if (membershipWasCreated)
        {
            store.Add(membership);
        }

        var roleAssignment = membershipWasCreated
            ? null
            : await store.GetActiveRoleAssignmentAsync(
                membership.Id,
                invitation.Role,
                cancellationToken);
        var roleAssignmentWasCreated = roleAssignment is null;
        roleAssignment ??= RoleAssignment.CreateActive(
            membership.Id,
            invitation.Role,
            nowUtc);
        if (roleAssignmentWasCreated)
        {
            store.Add(roleAssignment);
        }

        var result = invitation.Accept(membership.Id, roleAssignment.Id, nowUtc);
        if (result != InvitationAcceptanceResult.Accepted)
        {
            throw new InvalidOperationException("وضعیت دعوت هنگام پذیرش تغییر کرد.");
        }

        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CreateAcceptanceResult(invitation, organization.Name, false);
    }

    public async Task<DeclineInvitationResult> DeclineInvitationAsync(
        Guid userId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await store.BeginSerializableTransactionAsync(cancellationToken);
        var invitation = await store.GetInvitationForUpdateAsync(
            invitationId,
            userId,
            cancellationToken);
        if (invitation is null)
        {
            throw InvitationNotFoundException();
        }

        var status = invitation.Decline(nowUtc);
        await store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return status switch
        {
            InvitationStatus.Declined => new DeclineInvitationResult(invitation.Id, status),
            InvitationStatus.Expired => throw InvitationExpiredException(),
            InvitationStatus.Revoked => throw InvitationRevokedException(),
            InvitationStatus.Accepted => throw new OrganizationException(
                OrganizationErrorCodes.InvitationAlreadyAccepted,
                "این دعوت قبلاً پذیرفته شده است."),
            _ => throw InvitationNotFoundException()
        };
    }

    private async Task<MakanApp.Domain.Identity.UserSession> GetActiveSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await store.GetSessionAsync(sessionId, userId, cancellationToken);
        if (session is null || !session.IsActive(timeProvider.GetUtcNow().UtcDateTime))
        {
            throw new IdentityException(
                IdentityErrorCodes.AuthRequired,
                "برای ادامه باید وارد شوید.");
        }

        return session;
    }

    private static AccessContext CreatePersonalContext(Guid userId, Guid sessionId) =>
        new(
            userId,
            userId,
            sessionId,
            WorkspaceType.Personal,
            null,
            null,
            null);

    private static AcceptInvitationResult CreateAcceptanceResult(
        Invitation invitation,
        string organizationName,
        bool alreadyAccepted) =>
        new(
            invitation.Id,
            invitation.OrganizationId,
            organizationName,
            invitation.AcceptedMembershipId
                ?? throw new InvalidOperationException("عضویت پذیرفته‌شده ثبت نشده است."),
            invitation.AcceptedRoleAssignmentId
                ?? throw new InvalidOperationException("نقش پذیرفته‌شده ثبت نشده است."),
            invitation.Role,
            alreadyAccepted);

    private static OrganizationException WorkspaceNotFoundException() =>
        new(
            OrganizationErrorCodes.WorkspaceNotFound,
            "فضای کاری انتخاب‌شده پیدا نشد.");

    private static OrganizationException WorkspaceNotAllowedException() =>
        new(
            OrganizationErrorCodes.WorkspaceNotAllowed,
            "فضای کاری انتخاب‌شده دیگر مجاز نیست.");

    private static OrganizationException InvitationNotFoundException() =>
        new(
            OrganizationErrorCodes.InvitationNotFound,
            "دعوت موردنظر پیدا نشد.");

    private static OrganizationException InvitationExpiredException() =>
        new(
            OrganizationErrorCodes.InvitationExpired,
            "دعوت منقضی شده است.");

    private static OrganizationException InvitationRevokedException() =>
        new(
            OrganizationErrorCodes.InvitationRevoked,
            "دعوت لغو شده است.");
}
