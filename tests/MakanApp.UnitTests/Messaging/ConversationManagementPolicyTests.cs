using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class ConversationManagementPolicyTests
{
    private readonly ConversationManagementAuthorizationPolicy _policy = new();

    [Fact]
    public void PersonalCreationRequiresAdultInPersonalWorkspace()
    {
        var personal = Context(WorkspaceType.Personal, null, null);

        Assert.True(_policy.CanCreate(
            ConversationScope.Personal,
            personal,
            CommunicationAgeCategory.Adult));
        Assert.False(_policy.CanCreate(
            ConversationScope.Personal,
            personal,
            CommunicationAgeCategory.Minor));
    }

    [Theory]
    [InlineData(OrganizationRole.Manager, true)]
    [InlineData(OrganizationRole.Teacher, true)]
    [InlineData(OrganizationRole.Student, false)]
    [InlineData(OrganizationRole.Parent, false)]
    public void OrganizationCreationAllowsOnlyManagerOrTeacher(
        OrganizationRole role,
        bool expected)
    {
        var organizationId = Guid.NewGuid();
        var context = Context(WorkspaceType.Organization, organizationId, role);

        Assert.Equal(expected, _policy.CanCreate(
            ConversationScope.Organization,
            context,
            CommunicationAgeCategory.Adult));
    }

    [Fact]
    public void OwnerAndAdminHaveDifferentMembershipAuthority()
    {
        Assert.True(_policy.CanAddMember(ConversationParticipantRole.Owner));
        Assert.True(_policy.CanAddMember(ConversationParticipantRole.Admin));
        Assert.True(_policy.CanRemoveMember(
            ConversationParticipantRole.Owner,
            ConversationParticipantRole.Admin));
        Assert.False(_policy.CanRemoveMember(
            ConversationParticipantRole.Admin,
            ConversationParticipantRole.Admin));
        Assert.True(_policy.CanRemoveMember(
            ConversationParticipantRole.Admin,
            ConversationParticipantRole.Member));
        Assert.False(_policy.CanLeave(ConversationParticipantRole.Owner));
    }

    [Fact]
    public void ChannelPublishingIsLimitedToOwnerAndAdmin()
    {
        Assert.True(_policy.CanPublish(
            ConversationType.Channel,
            ConversationParticipantRole.Owner));
        Assert.True(_policy.CanPublish(
            ConversationType.Channel,
            ConversationParticipantRole.Admin));
        Assert.False(_policy.CanPublish(
            ConversationType.Channel,
            ConversationParticipantRole.Member));
        Assert.True(_policy.CanPublish(
            ConversationType.Group,
            ConversationParticipantRole.Member));
    }

    private static AccessContext Context(
        WorkspaceType workspaceType,
        Guid? organizationId,
        OrganizationRole? role)
    {
        var userId = Guid.NewGuid();
        return new AccessContext(
            userId,
            userId,
            Guid.NewGuid(),
            workspaceType,
            organizationId,
            organizationId.HasValue ? Guid.NewGuid() : null,
            role,
            null);
    }
}
