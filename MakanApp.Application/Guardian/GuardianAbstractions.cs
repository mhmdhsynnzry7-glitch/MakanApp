using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;

namespace MakanApp.Application.Guardian;

public interface IGuardianService
{
    Task<IReadOnlyCollection<AuthorizedChildResult>> GetMyAuthorizedChildrenAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<AccessContext> SelectChildContextAsync(
        Guid userId,
        Guid sessionId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken);

    Task<GuardianRelationSummaryResult> GetGuardianRelationSummaryAsync(
        Guid userId,
        Guid sessionId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken);
}

public interface IGuardianStore
{
    Task<IReadOnlyCollection<AuthorizedChildRecord>> GetAuthorizedChildrenAsync(
        Guid guardianUserId,
        Guid organizationId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<AuthorizedChildRecord?> GetAuthorizedChildAsync(
        Guid guardianUserId,
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<UserSession?> GetSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
