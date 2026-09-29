using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class OrganizationPersonPersistenceTests(MakanAppWebApplicationFactory factory)
{
    [Fact]
    public async Task PersonCanExistWithoutUserAccount()
    {
        var personId = await factory.CreatePersonWithoutUserAsync();

        var userCount = await factory.CountUsersForPersonAsync(personId);

        Assert.Equal(0, userCount);
    }

    [Fact]
    public async Task PersonCanHaveOrganizationRecord()
    {
        var personId = await factory.CreatePersonWithoutUserAsync();
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());

        await factory.CreateOrganizationPersonAsync(organizationId, personId);

        Assert.Equal(
            1,
            await factory.CountActiveOrganizationPersonsAsync(organizationId, personId));
    }

    [Fact]
    public async Task SamePersonCanHaveRecordsInTwoOrganizations()
    {
        var personId = await factory.CreatePersonWithoutUserAsync();
        var firstOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var secondOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());

        await factory.CreateOrganizationPersonAsync(firstOrganizationId, personId);
        await factory.CreateOrganizationPersonAsync(secondOrganizationId, personId);

        Assert.Equal(
            1,
            await factory.CountActiveOrganizationPersonsAsync(firstOrganizationId, personId));
        Assert.Equal(
            1,
            await factory.CountActiveOrganizationPersonsAsync(secondOrganizationId, personId));
    }

    [Fact]
    public async Task DatabasePreventsDuplicateActiveOrganizationPerson()
    {
        var personId = await factory.CreatePersonWithoutUserAsync();
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());

        var rejected = await factory.DuplicateActiveOrganizationPersonIsRejectedAsync(
            organizationId,
            personId);

        Assert.True(rejected);
    }

    [Fact]
    public async Task EndingOrganizationPersonKeepsHistoryAndAllowsNewActiveRecord()
    {
        var personId = await factory.CreatePersonWithoutUserAsync();
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var endedOrganizationPersonId = await factory.CreateOrganizationPersonAsync(
            organizationId,
            personId);
        await factory.EndOrganizationPersonAsync(endedOrganizationPersonId);

        await factory.CreateOrganizationPersonAsync(organizationId, personId);

        Assert.Equal(
            MakanApp.Domain.Organization.OrganizationPersonStatus.Ended,
            await factory.GetOrganizationPersonStatusAsync(endedOrganizationPersonId));
        Assert.Equal(2, await factory.CountOrganizationPersonsAsync(organizationId, personId));
        Assert.Equal(
            1,
            await factory.CountActiveOrganizationPersonsAsync(organizationId, personId));
    }

    [Fact]
    public async Task DatabaseRejectsInvalidOrganizationReference()
    {
        var personId = await factory.CreatePersonWithoutUserAsync();

        var rejected = await factory.InvalidOrganizationReferenceIsRejectedAsync(personId);

        Assert.True(rejected);
    }

    [Fact]
    public async Task DatabaseRejectsInvalidPersonReference()
    {
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());

        var rejected = await factory.InvalidPersonReferenceIsRejectedAsync(organizationId);

        Assert.True(rejected);
    }

    [Fact]
    public async Task DatabaseHasCompositeKeyForFutureSameOrganizationReferences()
    {
        Assert.True(await factory.HasOrganizationPersonCompositeKeyAsync());
    }

    private static string NewOrganizationName() => $"آموزشگاه {Guid.NewGuid():N}";
}
