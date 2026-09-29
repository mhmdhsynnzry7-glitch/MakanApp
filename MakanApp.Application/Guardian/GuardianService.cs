using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Guardian;

public sealed class GuardianService(
    IOrganizationService organizationService,
    IGuardianStore store,
    TimeProvider timeProvider) : IGuardianService
{
    public async Task<IReadOnlyCollection<AuthorizedChildResult>> GetMyAuthorizedChildrenAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await GetParentWorkspaceAsync(userId, sessionId, cancellationToken);
        var children = await store.GetAuthorizedChildrenAsync(
            userId,
            context.OrganizationId!.Value,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        return children
            .Select(child => new AuthorizedChildResult(
                child.LearnerOrganizationPersonId,
                child.DisplayName,
                child.OrganizationId,
                child.OrganizationName,
                child.Status))
            .ToArray();
    }

    public async Task<AccessContext> SelectChildContextAsync(
        Guid userId,
        Guid sessionId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken)
    {
        var context = await GetParentWorkspaceAsync(userId, sessionId, cancellationToken);
        var child = await store.GetAuthorizedChildAsync(
            userId,
            context.OrganizationId!.Value,
            learnerOrganizationPersonId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (child is null)
        {
            throw ChildContextNotFoundException();
        }

        var session = await store.GetSessionAsync(sessionId, userId, cancellationToken);
        if (session is null || !session.IsActive(timeProvider.GetUtcNow().UtcDateTime))
        {
            throw new IdentityException(
                IdentityErrorCodes.AuthRequired,
                "برای ادامه باید وارد شوید.");
        }

        session.SelectSubjectOrganizationPerson(child.LearnerOrganizationPersonId);
        await store.SaveChangesAsync(cancellationToken);
        return context with
        {
            SubjectOrganizationPersonId = child.LearnerOrganizationPersonId
        };
    }

    public async Task<GuardianRelationSummaryResult> GetGuardianRelationSummaryAsync(
        Guid userId,
        Guid sessionId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken)
    {
        var context = await GetParentWorkspaceAsync(userId, sessionId, cancellationToken);
        var child = await store.GetAuthorizedChildAsync(
            userId,
            context.OrganizationId!.Value,
            learnerOrganizationPersonId,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (child is null)
        {
            throw GuardianRelationNotFoundException();
        }

        return new GuardianRelationSummaryResult(
            child.RelationId,
            child.LearnerOrganizationPersonId,
            child.DisplayName,
            child.OrganizationId,
            child.OrganizationName,
            child.Status,
            child.ValidFromUtc,
            child.CreatedAtUtc);
    }

    private async Task<AccessContext> GetParentWorkspaceAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await organizationService.GetCurrentAccessContextAsync(
            userId,
            sessionId,
            cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            context.ActiveRole != OrganizationRole.Parent)
        {
            throw ParentRoleRequiredException();
        }

        return context;
    }

    private static GuardianException ParentRoleRequiredException() =>
        new(
            GuardianErrorCodes.ParentRoleRequired,
            "برای دسترسی به فرزند، فضای سازمانی با نقش فعال والد لازم است.");

    private static GuardianException ChildContextNotFoundException() =>
        new(
            GuardianErrorCodes.ChildContextNotFound,
            "فرزند مجاز در فضای سازمانی فعلی پیدا نشد.");

    private static GuardianException GuardianRelationNotFoundException() =>
        new(
            GuardianErrorCodes.GuardianRelationNotFound,
            "رابطه سرپرستی مجاز پیدا نشد.");
}
