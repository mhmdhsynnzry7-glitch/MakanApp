using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Guardian;

public sealed class GuardianAccessContextResolver(
    IOrganizationService organizationService,
    IGuardianStore store,
    TimeProvider timeProvider) : IAccessContextResolver
{
    public async Task<AccessContext> ResolveAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await organizationService.GetCurrentAccessContextAsync(
            userId,
            sessionId,
            cancellationToken);
        var session = await store.GetSessionAsync(sessionId, userId, cancellationToken);
        if (session is null || !session.IsActive(timeProvider.GetUtcNow().UtcDateTime))
        {
            throw new IdentityException(
                IdentityErrorCodes.AuthRequired,
                "برای ادامه باید وارد شوید.");
        }

        if (!session.SelectedSubjectOrganizationPersonId.HasValue)
        {
            return context;
        }

        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            context.ActiveRole != OrganizationRole.Parent)
        {
            throw ChildContextNotAllowedException();
        }

        var child = await store.GetAuthorizedChildAsync(
            userId,
            context.OrganizationId.Value,
            session.SelectedSubjectOrganizationPersonId.Value,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (child is null)
        {
            throw ChildContextNotAllowedException();
        }

        return context with
        {
            SubjectOrganizationPersonId = child.LearnerOrganizationPersonId
        };
    }

    private static GuardianException ChildContextNotAllowedException() =>
        new(
            GuardianErrorCodes.ChildContextNotAllowed,
            "بافت فرزند انتخاب‌شده دیگر مجاز نیست.");
}
