using System.Data;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MakanApp.Infrastructure.Organization;

public sealed class EfOrganizationStore(MakanDbContext dbContext) : IOrganizationStore
{
    public async Task<IOrganizationTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfOrganizationTransaction(transaction);
    }

    public async Task<IReadOnlyCollection<OrganizationWorkspaceRecord>> GetWorkspaceRecordsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await (
            from membership in dbContext.Memberships
            join organization in dbContext.Organizations
                on membership.OrganizationId equals organization.Id
            join roleAssignment in dbContext.RoleAssignments
                on membership.Id equals roleAssignment.MembershipId
            where membership.UserId == userId &&
                  membership.Status == MembershipStatus.Active &&
                  membership.EndedAtUtc == null &&
                  organization.Status == OrganizationStatus.Active &&
                  roleAssignment.Status == RoleAssignmentStatus.Active &&
                  roleAssignment.EndedAtUtc == null
            orderby organization.Name, roleAssignment.Role
            select new OrganizationWorkspaceRecord(
                organization.Id,
                organization.Name,
                membership.Id,
                roleAssignment.Role))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public Task<OrganizationWorkspaceRecord?> GetActiveWorkspaceRecordAsync(
        Guid userId,
        Guid membershipId,
        OrganizationRole role,
        CancellationToken cancellationToken) =>
        (
            from membership in dbContext.Memberships
            join organization in dbContext.Organizations
                on membership.OrganizationId equals organization.Id
            join roleAssignment in dbContext.RoleAssignments
                on membership.Id equals roleAssignment.MembershipId
            where membership.Id == membershipId &&
                  membership.UserId == userId &&
                  membership.Status == MembershipStatus.Active &&
                  membership.EndedAtUtc == null &&
                  organization.Status == OrganizationStatus.Active &&
                  roleAssignment.Role == role &&
                  roleAssignment.Status == RoleAssignmentStatus.Active &&
                  roleAssignment.EndedAtUtc == null
            select new OrganizationWorkspaceRecord(
                organization.Id,
                organization.Name,
                membership.Id,
                roleAssignment.Role))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Membership?> GetMembershipAsync(
        Guid membershipId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Memberships.SingleOrDefaultAsync(
            membership => membership.Id == membershipId && membership.UserId == userId,
            cancellationToken);

    public Task<Membership?> GetActiveMembershipAsync(
        Guid userId,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        dbContext.Memberships.SingleOrDefaultAsync(
            membership => membership.UserId == userId &&
                          membership.OrganizationId == organizationId &&
                          membership.Status == MembershipStatus.Active &&
                          membership.EndedAtUtc == null,
            cancellationToken);

    public Task<RoleAssignment?> GetActiveRoleAssignmentAsync(
        Guid membershipId,
        OrganizationRole role,
        CancellationToken cancellationToken) =>
        dbContext.RoleAssignments.SingleOrDefaultAsync(
            roleAssignment => roleAssignment.MembershipId == membershipId &&
                              roleAssignment.Role == role &&
                              roleAssignment.Status == RoleAssignmentStatus.Active &&
                              roleAssignment.EndedAtUtc == null,
            cancellationToken);

    public Task<MakanApp.Domain.Organization.Organization?> GetOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        dbContext.Organizations.SingleOrDefaultAsync(
            organization => organization.Id == organizationId,
            cancellationToken);

    public async Task<IReadOnlyCollection<InvitationWithOrganization>> GetInvitationsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await (
            from invitation in dbContext.Invitations
            join organization in dbContext.Organizations
                on invitation.OrganizationId equals organization.Id
            where invitation.DestinationUserId == userId
            orderby invitation.CreatedAtUtc descending
            select new InvitationWithOrganization(invitation, organization.Name))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public Task<Invitation?> GetInvitationForUpdateAsync(
        Guid invitationId,
        Guid destinationUserId,
        CancellationToken cancellationToken) =>
        dbContext.Invitations
            .FromSqlInterpolated(
                $"SELECT * FROM [organization].[Invitations] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {invitationId} AND [DestinationUserId] = {destinationUserId}")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<UserSession?> GetSessionAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.UserSessions.SingleOrDefaultAsync(
            session => session.Id == sessionId && session.UserId == userId,
            cancellationToken);

    public void Add(Membership membership) => dbContext.Memberships.Add(membership);
    public void Add(RoleAssignment roleAssignment) => dbContext.RoleAssignments.Add(roleAssignment);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new OrganizationException(
                OrganizationErrorCodes.ConcurrencyConflict,
                "اطلاعات هم‌زمان تغییر کرده است؛ دوباره تلاش کنید.");
        }
    }

    private sealed class EfOrganizationTransaction(IDbContextTransaction transaction)
        : IOrganizationTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
