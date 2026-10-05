using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Messaging;

public interface IConversationManagementPolicy
{
    bool CanCreate(
        ConversationScope scope,
        AccessContext accessContext,
        CommunicationAgeCategory actorAgeCategory);

    bool CanAddMember(ConversationParticipantRole actorRole);
    bool CanRemoveMember(ConversationParticipantRole actorRole, ConversationParticipantRole targetRole);
    bool CanLeave(ConversationParticipantRole actorRole);
    bool CanChangeRole(
        ConversationParticipantRole actorRole,
        ConversationParticipantRole targetRole,
        ConversationParticipantRole requestedRole);
    bool CanTransferOwnership(ConversationParticipantRole actorRole);
    bool CanArchive(ConversationParticipantRole actorRole);
    bool CanPublish(ConversationType type, ConversationParticipantRole actorRole);
}

public sealed class ConversationManagementAuthorizationPolicy : IConversationManagementPolicy
{
    public bool CanCreate(
        ConversationScope scope,
        AccessContext accessContext,
        CommunicationAgeCategory actorAgeCategory) =>
        scope switch
        {
            ConversationScope.Personal =>
                accessContext.WorkspaceType == WorkspaceType.Personal &&
                !accessContext.OrganizationId.HasValue &&
                actorAgeCategory == CommunicationAgeCategory.Adult,
            ConversationScope.Organization =>
                accessContext.WorkspaceType == WorkspaceType.Organization &&
                accessContext.OrganizationId.HasValue &&
                accessContext.MembershipId.HasValue &&
                accessContext.ActiveRole is OrganizationRole.Manager or OrganizationRole.Teacher,
            _ => false
        };

    public bool CanAddMember(ConversationParticipantRole actorRole) =>
        actorRole is ConversationParticipantRole.Owner or ConversationParticipantRole.Admin;

    public bool CanRemoveMember(
        ConversationParticipantRole actorRole,
        ConversationParticipantRole targetRole) =>
        actorRole == ConversationParticipantRole.Owner && targetRole != ConversationParticipantRole.Owner ||
        actorRole == ConversationParticipantRole.Admin && targetRole == ConversationParticipantRole.Member;

    public bool CanLeave(ConversationParticipantRole actorRole) =>
        actorRole != ConversationParticipantRole.Owner;

    public bool CanChangeRole(
        ConversationParticipantRole actorRole,
        ConversationParticipantRole targetRole,
        ConversationParticipantRole requestedRole) =>
        actorRole == ConversationParticipantRole.Owner &&
        targetRole != ConversationParticipantRole.Owner &&
        requestedRole is ConversationParticipantRole.Admin or ConversationParticipantRole.Member;

    public bool CanTransferOwnership(ConversationParticipantRole actorRole) =>
        actorRole == ConversationParticipantRole.Owner;

    public bool CanArchive(ConversationParticipantRole actorRole) =>
        actorRole == ConversationParticipantRole.Owner;

    public bool CanPublish(ConversationType type, ConversationParticipantRole actorRole) =>
        type switch
        {
            ConversationType.Group => true,
            ConversationType.Channel => actorRole is ConversationParticipantRole.Owner or ConversationParticipantRole.Admin,
            _ => false
        };
}
