using System.Security.Cryptography;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.UnitTests.Messaging;

public sealed class MessagingModelTests
{
    [Fact]
    public void UserManagedConversationRequiresTitleAndTracksCreationIdentity()
    {
        var creatorId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var hash = RandomNumberGenerator.GetBytes(Conversation.CreationPayloadHashLength);

        var conversation = Conversation.CreateUserManaged(
            ConversationType.Group,
            ConversationScope.Personal,
            null,
            "  گروه مطالعه  ",
            "  توضیح  ",
            creatorId,
            operationId,
            hash,
            DateTime.UtcNow);

        Assert.Equal("گروه مطالعه", conversation.Title);
        Assert.Equal("توضیح", conversation.Description);
        Assert.Equal(ConversationManagementPolicy.UserManaged, conversation.ManagementPolicy);
        Assert.Equal(creatorId, conversation.CreatedByUserId);
        Assert.Equal(operationId, conversation.ClientOperationId);
        Assert.Throws<ArgumentException>(() => Conversation.CreateUserManaged(
            ConversationType.Channel,
            ConversationScope.Personal,
            null,
            " ",
            null,
            creatorId,
            Guid.NewGuid(),
            hash,
            DateTime.UtcNow));
    }

    [Fact]
    public void ParticipantLifecyclePreservesEndedMembershipAndRole()
    {
        var userId = Guid.NewGuid();
        var participant = ConversationParticipant.CreateActive(
            Guid.NewGuid(),
            userId,
            DateTime.UtcNow,
            ConversationParticipantRole.Admin);

        participant.Leave(DateTime.UtcNow);

        Assert.False(participant.IsActive);
        Assert.Equal(ConversationParticipantStatus.Left, participant.Status);
        Assert.Equal(userId, participant.EndedByUserId);
        Assert.Throws<InvalidOperationException>(() =>
            participant.ChangeRole(ConversationParticipantRole.Member));
    }

    [Fact]
    public void OwnershipTransferChangesStateOnlyAfterDestinationAcceptance()
    {
        var transfer = ConversationOwnershipTransfer.CreatePending(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        Assert.Equal(OwnershipTransferStatus.Pending, transfer.Status);
        transfer.Accept(DateTime.UtcNow);
        Assert.Equal(OwnershipTransferStatus.Accepted, transfer.Status);
        Assert.Throws<InvalidOperationException>(() => transfer.Decline(DateTime.UtcNow));
    }

    [Fact]
    public void DirectPairIsCanonicalRegardlessOfInputOrder()
    {
        var first = Guid.Parse("10000000-0000-0000-0000-000000000000");
        var second = Guid.Parse("20000000-0000-0000-0000-000000000000");

        Assert.Equal(DirectUserPair.Create(first, second), DirectUserPair.Create(second, first));
    }

    [Fact]
    public void DirectPairRejectsConversationWithSelf()
    {
        var userId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => DirectUserPair.Create(userId, userId));
    }

    [Fact]
    public void ConversationAllocatesStrictlyIncreasingSequences()
    {
        var conversation = Conversation.CreateDirect(
            ConversationScope.Personal,
            null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow);

        Assert.Equal(1, conversation.AllocateNextMessageSequence());
        Assert.Equal(2, conversation.AllocateNextMessageSequence());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MessageRejectsEmptyText(string text)
    {
        Assert.Throws<ArgumentException>(() => CreateMessage(text, 4000));
    }

    [Fact]
    public void MessageRejectsTextAboveConfiguredLimit()
    {
        Assert.Throws<ArgumentException>(() => CreateMessage("123456", 5));
    }

    [Fact]
    public void MessagePreservesExactText()
    {
        const string text = "  <b>سلام</b>  ";

        var message = CreateMessage(text, 4000);

        Assert.Equal(text, message.Text);
    }

    private static Message CreateMessage(string text, int maximumLength) =>
        Message.CreateText(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            text,
            maximumLength,
            DateTime.UtcNow);
}

public sealed class CommunicationEligibilityPolicyTests
{
    private readonly CommunicationEligibilityPolicy _policy = new();

    [Fact]
    public void PersonalConversationRequiresTwoAdultsAndActiveGrant()
    {
        var allowed = CreateFacts(
            ConversationScope.Personal,
            actorAge: CommunicationAgeCategory.Adult,
            targetAge: CommunicationAgeCategory.Adult,
            hasGrant: true);
        var noGrant = allowed with { HasActivePersonalGrant = false };
        var minor = allowed with { TargetAgeCategory = CommunicationAgeCategory.Minor };

        Assert.True(_policy.CanStartOrSend(allowed, MessagingEligibilityOperation.Start));
        Assert.False(_policy.CanStartOrSend(noGrant, MessagingEligibilityOperation.Start));
        Assert.False(_policy.CanStartOrSend(minor, MessagingEligibilityOperation.Start));
    }

    [Fact]
    public void TeacherAndStudentRequireSharedActiveClassRelationship()
    {
        var related = CreateFacts(
            ConversationScope.Organization,
            actorRole: OrganizationRole.Teacher,
            targetRole: OrganizationRole.Student,
            targetHasEnrollment: true,
            teacherStudentRelationship: true);

        Assert.True(_policy.CanStartOrSend(related, MessagingEligibilityOperation.Start));
        Assert.False(_policy.CanStartOrSend(
            related with { HasActiveTeacherStudentRelationship = false },
            MessagingEligibilityOperation.Start));
    }

    [Fact]
    public void StudentCannotStartConversationWithStudent()
    {
        var facts = CreateFacts(
            ConversationScope.Organization,
            actorRole: OrganizationRole.Student,
            targetRole: OrganizationRole.Student,
            actorHasEnrollment: true,
            targetHasEnrollment: true);

        Assert.False(_policy.CanStartOrSend(facts, MessagingEligibilityOperation.Start));
    }

    [Fact]
    public void OrganizationConversationRequiresMatchingAccessContext()
    {
        var facts = CreateFacts(
            ConversationScope.Organization,
            actorRole: OrganizationRole.Manager,
            targetRole: OrganizationRole.Teacher);

        Assert.False(_policy.CanStartOrSend(
            facts with
            {
                AccessContext = facts.AccessContext with { OrganizationId = Guid.NewGuid() }
            },
            MessagingEligibilityOperation.Start));
    }

    [Fact]
    public void PersonalHistoryRemainsReadableAfterEligibilityChanges()
    {
        var facts = new ConversationReadFacts(
            ConversationScope.Personal,
            null,
            PersonalContext(),
            true,
            false);

        Assert.True(_policy.CanRead(facts));
    }

    private static CommunicationEligibilityFacts CreateFacts(
        ConversationScope scope,
        CommunicationAgeCategory actorAge = CommunicationAgeCategory.Unknown,
        CommunicationAgeCategory targetAge = CommunicationAgeCategory.Unknown,
        bool hasGrant = false,
        OrganizationRole actorRole = OrganizationRole.Manager,
        OrganizationRole targetRole = OrganizationRole.Teacher,
        bool actorHasEnrollment = false,
        bool targetHasEnrollment = false,
        bool teacherStudentRelationship = false)
    {
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var organizationId = scope == ConversationScope.Organization ? Guid.NewGuid() : (Guid?)null;
        var context = scope == ConversationScope.Organization
            ? new AccessContext(
                actorId,
                actorId,
                Guid.NewGuid(),
                WorkspaceType.Organization,
                organizationId,
                Guid.NewGuid(),
                actorRole,
                null)
            : PersonalContext(actorId);
        return new CommunicationEligibilityFacts(
            actorId,
            targetId,
            scope,
            organizationId,
            context,
            true,
            true,
            actorAge,
            targetAge,
            hasGrant,
            scope == ConversationScope.Organization,
            scope == ConversationScope.Organization,
            scope == ConversationScope.Organization,
            new HashSet<OrganizationRole> { targetRole },
            actorHasEnrollment,
            targetHasEnrollment,
            teacherStudentRelationship,
            false);
    }

    private static AccessContext PersonalContext(Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        return new AccessContext(
            id,
            id,
            Guid.NewGuid(),
            WorkspaceType.Personal,
            null,
            null,
            null,
            null);
    }
}
