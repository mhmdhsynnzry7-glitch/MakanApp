using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Organization;

public sealed class MembershipAndWorkspaceTests
{
    private static readonly DateTime NowUtc =
        new(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void EndedMembershipIsInactive()
    {
        var membership = Membership.CreateActive(Guid.NewGuid(), Guid.NewGuid(), NowUtc);

        membership.End(NowUtc.AddDays(1));

        Assert.False(membership.IsActive);
        Assert.Equal(MembershipStatus.Ended, membership.Status);
    }

    [Fact]
    public void EndingOneMembershipDoesNotAffectAnother()
    {
        var userId = Guid.NewGuid();
        var first = Membership.CreateActive(userId, Guid.NewGuid(), NowUtc);
        var second = Membership.CreateActive(userId, Guid.NewGuid(), NowUtc);

        first.End(NowUtc.AddDays(1));

        Assert.False(first.IsActive);
        Assert.True(second.IsActive);
    }

    [Fact]
    public void RoleAssignmentBelongsToMembership()
    {
        var membershipId = Guid.NewGuid();

        var role = RoleAssignment.CreateActive(
            membershipId,
            OrganizationRole.Parent,
            NowUtc);

        Assert.Equal(membershipId, role.MembershipId);
        Assert.Equal(OrganizationRole.Parent, role.Role);
        Assert.True(role.IsActive);
    }

    [Fact]
    public void PersonalWorkspaceExistsWithoutOrganizationMembership()
    {
        var workspace = WorkspaceFactory.CreatePersonal();

        Assert.Equal(WorkspaceType.Personal, workspace.WorkspaceType);
        Assert.Null(workspace.OrganizationId);
        Assert.Null(workspace.MembershipId);
        Assert.Null(workspace.Role);
    }
}
