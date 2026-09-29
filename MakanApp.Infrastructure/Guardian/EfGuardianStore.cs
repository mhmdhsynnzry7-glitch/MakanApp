using MakanApp.Application.Guardian;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Guardian;

public sealed class EfGuardianStore(MakanDbContext dbContext) : IGuardianStore
{
    public async Task<IReadOnlyCollection<AuthorizedChildRecord>> GetAuthorizedChildrenAsync(
        Guid guardianUserId,
        Guid organizationId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var children = await CreateAuthorizedChildrenQuery(guardianUserId, organizationId, nowUtc)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return children
            .OrderBy(child => child.DisplayName)
            .ThenBy(child => child.LearnerOrganizationPersonId)
            .ToArray();
    }

    public Task<AuthorizedChildRecord?> GetAuthorizedChildAsync(
        Guid guardianUserId,
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        CreateAuthorizedChildrenQuery(
                guardianUserId,
                organizationId,
                nowUtc,
                learnerOrganizationPersonId)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public Task<UserSession?> GetSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.UserSessions.SingleOrDefaultAsync(
            session => session.Id == sessionId && session.UserId == userId,
            cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GuardianException(
                GuardianErrorCodes.ConcurrencyConflict,
                "اطلاعات رابطه سرپرستی هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.");
        }
    }

    private IQueryable<AuthorizedChildRecord> CreateAuthorizedChildrenQuery(
        Guid guardianUserId,
        Guid organizationId,
        DateTime nowUtc,
        Guid? learnerOrganizationPersonId = null) =>
        from relation in dbContext.GuardianRelations
        join organizationPerson in dbContext.OrganizationPersons
            on new
            {
                relation.OrganizationId,
                OrganizationPersonId = relation.LearnerOrganizationPersonId
            }
            equals new
            {
                organizationPerson.OrganizationId,
                OrganizationPersonId = organizationPerson.Id
            }
        join person in dbContext.Persons
            on organizationPerson.PersonId equals person.Id
        join organization in dbContext.Organizations
            on relation.OrganizationId equals organization.Id
        where relation.GuardianUserId == guardianUserId &&
              relation.OrganizationId == organizationId &&
              (!learnerOrganizationPersonId.HasValue ||
               relation.LearnerOrganizationPersonId == learnerOrganizationPersonId.Value) &&
              relation.Status == GuardianRelationStatus.Active &&
              relation.EndedAtUtc == null &&
              relation.ValidFromUtc <= nowUtc &&
              organizationPerson.Status == OrganizationPersonStatus.Active &&
              organizationPerson.EndedAtUtc == null &&
              organization.Status == OrganizationStatus.Active
        select new AuthorizedChildRecord(
            relation.Id,
            organizationPerson.Id,
            person.DisplayName ?? person.FirstName + " " + person.LastName,
            organization.Id,
            organization.Name,
            relation.Status,
            relation.ValidFromUtc,
            relation.CreatedAtUtc);
}
