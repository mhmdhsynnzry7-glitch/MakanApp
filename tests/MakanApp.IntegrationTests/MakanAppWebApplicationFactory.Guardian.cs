using MakanApp.Domain.Guardian;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<Guid> CreateGuardianRelationAsync(
        Guid guardianUserId,
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        GuardianRelationStatus status = GuardianRelationStatus.Active)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        var relation = status == GuardianRelationStatus.Pending
            ? GuardianRelation.CreatePending(
                organizationId,
                guardianUserId,
                learnerOrganizationPersonId,
                nowUtc)
            : GuardianRelation.CreateActive(
                organizationId,
                guardianUserId,
                learnerOrganizationPersonId,
                nowUtc);

        if (status == GuardianRelationStatus.Ended)
        {
            relation.End(nowUtc);
        }
        else if (status == GuardianRelationStatus.Revoked)
        {
            relation.Revoke(nowUtc);
        }

        dbContext.GuardianRelations.Add(relation);
        await dbContext.SaveChangesAsync();
        return relation.Id;
    }

    public async Task RevokeGuardianRelationAsync(Guid relationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var relation = await dbContext.GuardianRelations.SingleAsync(item => item.Id == relationId);
        relation.Revoke(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    public async Task<int> CountActiveGuardianRelationsAsync(
        Guid guardianUserId,
        Guid organizationId,
        Guid learnerOrganizationPersonId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.GuardianRelations.CountAsync(relation =>
            relation.GuardianUserId == guardianUserId &&
            relation.OrganizationId == organizationId &&
            relation.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
            relation.Status == GuardianRelationStatus.Active &&
            relation.EndedAtUtc == null);
    }

    public async Task<bool> DuplicateActiveGuardianRelationIsRejectedAsync(
        Guid guardianUserId,
        Guid organizationId,
        Guid learnerOrganizationPersonId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        dbContext.GuardianRelations.AddRange(
            GuardianRelation.CreateActive(
                organizationId,
                guardianUserId,
                learnerOrganizationPersonId,
                nowUtc),
            GuardianRelation.CreateActive(
                organizationId,
                guardianUserId,
                learnerOrganizationPersonId,
                nowUtc));

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

    public async Task<bool> CrossOrganizationGuardianRelationIsRejectedAsync(
        Guid guardianUserId,
        Guid organizationId,
        Guid learnerOrganizationPersonId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        dbContext.GuardianRelations.Add(
            GuardianRelation.CreateActive(
                organizationId,
                guardianUserId,
                learnerOrganizationPersonId,
                DateTime.UtcNow));

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

    public async Task<(int SuccessfulWrites, int ActiveRelations)>
        CreateGuardianRelationsConcurrentlyAsync(
            Guid guardianUserId,
            Guid organizationId,
            Guid learnerOrganizationPersonId)
    {
        async Task<bool> CreateAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
            dbContext.GuardianRelations.Add(
                GuardianRelation.CreateActive(
                    organizationId,
                    guardianUserId,
                    learnerOrganizationPersonId,
                    DateTime.UtcNow));
            try
            {
                await dbContext.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(CreateAsync(), CreateAsync());
        return (
            results.Count(success => success),
            await CountActiveGuardianRelationsAsync(
                guardianUserId,
                organizationId,
                learnerOrganizationPersonId));
    }

    public async Task<bool> GuardianRelationRowVersionDetectsStaleWriteAsync(Guid relationId)
    {
        await using var firstScope = Services.CreateAsyncScope();
        await using var secondScope = Services.CreateAsyncScope();
        var firstDbContext = firstScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var secondDbContext = secondScope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var first = await firstDbContext.GuardianRelations.SingleAsync(item => item.Id == relationId);
        var second = await secondDbContext.GuardianRelations.SingleAsync(item => item.Id == relationId);

        first.End(DateTime.UtcNow);
        await firstDbContext.SaveChangesAsync();
        second.Revoke(DateTime.UtcNow);
        try
        {
            await secondDbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateConcurrencyException)
        {
            return true;
        }
    }
}
