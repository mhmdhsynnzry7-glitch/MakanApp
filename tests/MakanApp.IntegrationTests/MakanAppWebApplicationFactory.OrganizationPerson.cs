using System.Data;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<Guid> CreatePersonWithoutUserAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var person = Person.Create(
            "دانش‌آموز",
            Guid.NewGuid().ToString("N"),
            "دانش‌آموز آزمایشی",
            DateTime.UtcNow);
        dbContext.Persons.Add(person);
        await dbContext.SaveChangesAsync();
        return person.Id;
    }

    public async Task<Guid> CreateOrganizationPersonAsync(
        Guid organizationId,
        Guid personId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var organizationPerson = OrganizationPerson.CreateActive(
            organizationId,
            personId,
            DateTime.UtcNow);
        dbContext.OrganizationPersons.Add(organizationPerson);
        await dbContext.SaveChangesAsync();
        return organizationPerson.Id;
    }

    public async Task EndOrganizationPersonAsync(Guid organizationPersonId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var organizationPerson = await dbContext.OrganizationPersons.SingleAsync(
            item => item.Id == organizationPersonId);
        organizationPerson.End(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    public async Task<int> CountUsersForPersonAsync(Guid personId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.Users.CountAsync(user => user.PersonId == personId);
    }

    public async Task<int> CountOrganizationPersonsAsync(
        Guid organizationId,
        Guid personId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.OrganizationPersons.CountAsync(
            item => item.OrganizationId == organizationId && item.PersonId == personId);
    }

    public async Task<int> CountActiveOrganizationPersonsAsync(
        Guid organizationId,
        Guid personId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.OrganizationPersons.CountAsync(
            item => item.OrganizationId == organizationId &&
                    item.PersonId == personId &&
                    item.Status == OrganizationPersonStatus.Active &&
                    item.EndedAtUtc == null);
    }

    public async Task<OrganizationPersonStatus> GetOrganizationPersonStatusAsync(
        Guid organizationPersonId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.OrganizationPersons
            .Where(item => item.Id == organizationPersonId)
            .Select(item => item.Status)
            .SingleAsync();
    }

    public async Task<bool> DuplicateActiveOrganizationPersonIsRejectedAsync(
        Guid organizationId,
        Guid personId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var nowUtc = DateTime.UtcNow;
        dbContext.OrganizationPersons.Add(
            OrganizationPerson.CreateActive(organizationId, personId, nowUtc));
        dbContext.OrganizationPersons.Add(
            OrganizationPerson.CreateActive(organizationId, personId, nowUtc));
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

    public async Task<bool> InvalidOrganizationReferenceIsRejectedAsync(Guid personId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        dbContext.OrganizationPersons.Add(
            OrganizationPerson.CreateActive(Guid.NewGuid(), personId, DateTime.UtcNow));
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

    public async Task<bool> InvalidPersonReferenceIsRejectedAsync(Guid organizationId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        dbContext.OrganizationPersons.Add(
            OrganizationPerson.CreateActive(organizationId, Guid.NewGuid(), DateTime.UtcNow));
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

    public async Task<bool> HasOrganizationPersonCompositeKeyAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sys.key_constraints AS key_constraint
            INNER JOIN sys.tables AS table_info
                ON table_info.object_id = key_constraint.parent_object_id
            INNER JOIN sys.schemas AS schema_info
                ON schema_info.schema_id = table_info.schema_id
            INNER JOIN sys.index_columns AS index_column
                ON index_column.object_id = table_info.object_id
                AND index_column.index_id = key_constraint.unique_index_id
            INNER JOIN sys.columns AS column_info
                ON column_info.object_id = table_info.object_id
                AND column_info.column_id = index_column.column_id
            WHERE schema_info.name = N'organization'
                AND table_info.name = N'OrganizationPersons'
                AND key_constraint.name = N'UQ_OrganizationPersons_OrganizationId_Id'
                AND column_info.name IN (N'OrganizationId', N'Id');
            """;
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) == 2;
    }
}
