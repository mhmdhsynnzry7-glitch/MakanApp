using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Organization;

public sealed class InvitationTests
{
    private static readonly DateTime CreatedAtUtc =
        new(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ExpiredInvitationCannotBeAccepted()
    {
        var invitation = CreateInvitation();

        var result = invitation.Accept(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatedAtUtc.AddHours(2));

        Assert.Equal(InvitationAcceptanceResult.Expired, result);
        Assert.Equal(InvitationStatus.Expired, invitation.Status);
        Assert.Null(invitation.AcceptedMembershipId);
    }

    [Fact]
    public void RevokedInvitationCannotBeAccepted()
    {
        var invitation = CreateInvitation();
        invitation.Revoke(CreatedAtUtc.AddMinutes(10));

        var result = invitation.Accept(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatedAtUtc.AddMinutes(20));

        Assert.Equal(InvitationAcceptanceResult.Revoked, result);
        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
    }

    [Fact]
    public void AcceptedInvitationReturnsSameEffectOnSecondAcceptance()
    {
        var invitation = CreateInvitation();
        var membershipId = Guid.NewGuid();
        var roleAssignmentId = Guid.NewGuid();

        var first = invitation.Accept(
            membershipId,
            roleAssignmentId,
            CreatedAtUtc.AddMinutes(10));
        var second = invitation.Accept(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreatedAtUtc.AddMinutes(20));

        Assert.Equal(InvitationAcceptanceResult.Accepted, first);
        Assert.Equal(InvitationAcceptanceResult.AlreadyAccepted, second);
        Assert.Equal(membershipId, invitation.AcceptedMembershipId);
        Assert.Equal(roleAssignmentId, invitation.AcceptedRoleAssignmentId);
    }

    private static Invitation CreateInvitation() =>
        Invitation.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            OrganizationRole.Teacher,
            CreatedAtUtc,
            CreatedAtUtc.AddHours(1));
}
