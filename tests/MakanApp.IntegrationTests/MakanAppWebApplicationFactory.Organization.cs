using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<Guid> CreateOrganizationAsync(string name)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var organization = MakanApp.Domain.Organization.Organization.Create(name, DateTime.UtcNow);
        dbContext.Organizations.Add(organization);
        await dbContext.SaveChangesAsync();
        return organization.Id;
    }

    public async Task<(Guid MembershipId, Guid RoleAssignmentId)> CreateMembershipAsync(
        Guid userId,
        Guid organizationId,
        OrganizationRole role)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        var membership = Membership.CreateActive(userId, organizationId, nowUtc);
        var roleAssignment = RoleAssignment.CreateActive(membership.Id, role, nowUtc);
        dbContext.Memberships.Add(membership);
        dbContext.RoleAssignments.Add(roleAssignment);
        await dbContext.SaveChangesAsync();
        return (membership.Id, roleAssignment.Id);
    }

    public async Task<Guid> CreateInvitationAsync(
        Guid destinationUserId,
        Guid organizationId,
        OrganizationRole role,
        bool expired = false,
        bool revoked = false)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        var createdAtUtc = expired ? nowUtc.AddHours(-2) : nowUtc;
        var expiresAtUtc = expired ? nowUtc.AddHours(-1) : nowUtc.AddDays(1);
        var invitation = Invitation.Create(
            organizationId,
            destinationUserId,
            destinationUserId,
            role,
            createdAtUtc,
            expiresAtUtc);
        if (revoked)
        {
            invitation.Revoke(nowUtc);
        }

        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync();
        return invitation.Id;
    }

    public async Task EndMembershipAsync(Guid membershipId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var membership = await dbContext.Memberships.SingleAsync(item => item.Id == membershipId);
        membership.End(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    public async Task EndRoleAssignmentAsync(Guid roleAssignmentId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var roleAssignment = await dbContext.RoleAssignments
            .SingleAsync(item => item.Id == roleAssignmentId);
        roleAssignment.End(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    public async Task<int> CountMembershipsAsync(Guid userId, Guid organizationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Memberships.CountAsync(
            membership => membership.UserId == userId &&
                          membership.OrganizationId == organizationId);
    }

    public async Task<int> CountRoleAssignmentsAsync(
        Guid membershipId,
        OrganizationRole role)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.RoleAssignments.CountAsync(
            roleAssignment => roleAssignment.MembershipId == membershipId &&
                              roleAssignment.Role == role);
    }

    public async Task<bool> ActiveMembershipUniquenessIsEnforcedAsync(
        Guid userId,
        Guid organizationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        dbContext.Memberships.Add(Membership.CreateActive(userId, organizationId, nowUtc));
        dbContext.Memberships.Add(Membership.CreateActive(userId, organizationId, nowUtc));
        try
        {
            await dbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }
}
