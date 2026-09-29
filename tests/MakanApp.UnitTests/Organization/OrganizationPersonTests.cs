using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Organization;

public sealed class OrganizationPersonTests
{
    [Fact]
    public void NewOrganizationPersonIsActive()
    {
        var organizationId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var createdAtUtc = DateTime.UtcNow;

        var organizationPerson = OrganizationPerson.CreateActive(
            organizationId,
            personId,
            createdAtUtc);

        Assert.True(organizationPerson.IsActive);
        Assert.Equal(OrganizationPersonStatus.Active, organizationPerson.Status);
        Assert.Equal(organizationId, organizationPerson.OrganizationId);
        Assert.Equal(personId, organizationPerson.PersonId);
        Assert.Equal(createdAtUtc, organizationPerson.ActivatedAtUtc);
        Assert.Null(organizationPerson.EndedAtUtc);
    }

    [Fact]
    public void EndingOrganizationPersonPreservesItsIdentityAndHistory()
    {
        var organizationId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var createdAtUtc = DateTime.UtcNow.AddDays(-1);
        var endedAtUtc = DateTime.UtcNow;
        var organizationPerson = OrganizationPerson.CreateActive(
            organizationId,
            personId,
            createdAtUtc);
        var id = organizationPerson.Id;

        organizationPerson.End(endedAtUtc);

        Assert.False(organizationPerson.IsActive);
        Assert.Equal(OrganizationPersonStatus.Ended, organizationPerson.Status);
        Assert.Equal(endedAtUtc, organizationPerson.EndedAtUtc);
        Assert.Equal(id, organizationPerson.Id);
        Assert.Equal(organizationId, organizationPerson.OrganizationId);
        Assert.Equal(personId, organizationPerson.PersonId);
        Assert.Equal(createdAtUtc, organizationPerson.CreatedAtUtc);
    }
}
