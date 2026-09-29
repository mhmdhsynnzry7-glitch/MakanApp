using MakanApp.Domain.Guardian;
using Xunit;

namespace MakanApp.UnitTests.Guardian;

public sealed class GuardianRelationTests
{
    [Fact]
    public void PendingRelationDoesNotAuthorize()
    {
        var relation = CreatePending();

        Assert.False(relation.IsActiveAt(DateTime.UtcNow));
    }

    [Fact]
    public void ActiveRelationIsEligible()
    {
        var nowUtc = DateTime.UtcNow;
        var relation = CreateActive(nowUtc.AddMinutes(-1));

        Assert.True(relation.IsActiveAt(nowUtc));
    }

    [Fact]
    public void RevokedRelationDoesNotAuthorize()
    {
        var nowUtc = DateTime.UtcNow;
        var relation = CreateActive(nowUtc.AddMinutes(-1));

        relation.Revoke(nowUtc);

        Assert.False(relation.IsActiveAt(nowUtc));
        Assert.Equal(GuardianRelationStatus.Revoked, relation.Status);
        Assert.Equal(nowUtc, relation.EndedAtUtc);
    }

    [Fact]
    public void EndedRelationDoesNotAuthorize()
    {
        var nowUtc = DateTime.UtcNow;
        var relation = CreateActive(nowUtc.AddMinutes(-1));

        relation.End(nowUtc);

        Assert.False(relation.IsActiveAt(nowUtc));
        Assert.Equal(GuardianRelationStatus.Ended, relation.Status);
    }

    [Fact]
    public void EndingFirstLearnerRelationDoesNotAffectSecond()
    {
        var nowUtc = DateTime.UtcNow;
        var first = CreateActive(nowUtc.AddMinutes(-1));
        var second = CreateActive(nowUtc.AddMinutes(-1));

        first.End(nowUtc);

        Assert.False(first.IsActiveAt(nowUtc));
        Assert.True(second.IsActiveAt(nowUtc));
    }

    [Fact]
    public void RelationRetainsItsOrganizationScope()
    {
        var organizationId = Guid.NewGuid();
        var learnerOrganizationPersonId = Guid.NewGuid();
        var relation = GuardianRelation.CreateActive(
            organizationId,
            Guid.NewGuid(),
            learnerOrganizationPersonId,
            DateTime.UtcNow);

        Assert.Equal(organizationId, relation.OrganizationId);
        Assert.Equal(learnerOrganizationPersonId, relation.LearnerOrganizationPersonId);
    }

    [Fact]
    public void DuplicateActivationIsRejectedByDomainLifecycle()
    {
        var relation = CreateActive(DateTime.UtcNow.AddMinutes(-1));

        Assert.Throws<InvalidOperationException>(() => relation.Activate(DateTime.UtcNow));
    }

    private static GuardianRelation CreatePending() =>
        GuardianRelation.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddMinutes(-1));

    private static GuardianRelation CreateActive(DateTime createdAtUtc) =>
        GuardianRelation.CreateActive(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            createdAtUtc);
}
