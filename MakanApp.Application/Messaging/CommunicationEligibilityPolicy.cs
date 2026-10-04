using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Messaging;

public enum MessagingEligibilityOperation
{
    Start = 1,
    Send = 2
}

public sealed record CommunicationEligibilityFacts(
    Guid ActorUserId,
    Guid TargetUserId,
    ConversationScope Scope,
    Guid? OrganizationId,
    AccessContext AccessContext,
    bool ActorAccountAvailable,
    bool TargetAccountAvailable,
    CommunicationAgeCategory ActorAgeCategory,
    CommunicationAgeCategory TargetAgeCategory,
    bool HasActivePersonalGrant,
    bool ActorMembershipActive,
    bool ActorRoleActive,
    bool TargetMembershipActive,
    IReadOnlySet<OrganizationRole> TargetActiveRoles,
    bool ActorHasActiveEnrollment,
    bool TargetHasActiveEnrollment,
    bool HasActiveTeacherStudentRelationship,
    bool HasActiveTeacherParentRelationship);

public sealed record ConversationReadFacts(
    ConversationScope Scope,
    Guid? OrganizationId,
    AccessContext AccessContext,
    bool IsActiveParticipant,
    bool HasActiveOrganizationMembership);

public interface ICommunicationEligibilityPolicy
{
    bool CanStartOrSend(
        CommunicationEligibilityFacts facts,
        MessagingEligibilityOperation operation);

    bool CanRead(ConversationReadFacts facts);
}

public sealed class CommunicationEligibilityPolicy : ICommunicationEligibilityPolicy
{
    public bool CanStartOrSend(
        CommunicationEligibilityFacts facts,
        MessagingEligibilityOperation operation)
    {
        if (facts.ActorUserId == Guid.Empty ||
            facts.TargetUserId == Guid.Empty ||
            facts.ActorUserId == facts.TargetUserId ||
            !facts.ActorAccountAvailable ||
            !facts.TargetAccountAvailable)
        {
            return false;
        }

        return facts.Scope switch
        {
            ConversationScope.Personal => CanUsePersonal(facts, operation),
            ConversationScope.Organization => CanUseOrganization(facts),
            _ => false
        };
    }

    public bool CanRead(ConversationReadFacts facts)
    {
        if (!facts.IsActiveParticipant)
        {
            return false;
        }

        if (facts.Scope == ConversationScope.Personal)
        {
            return true;
        }

        return facts.Scope == ConversationScope.Organization &&
               facts.OrganizationId.HasValue &&
               facts.AccessContext.WorkspaceType == WorkspaceType.Organization &&
               facts.AccessContext.OrganizationId == facts.OrganizationId &&
               facts.HasActiveOrganizationMembership;
    }

    private static bool CanUsePersonal(
        CommunicationEligibilityFacts facts,
        MessagingEligibilityOperation operation) =>
        (!operation.Equals(MessagingEligibilityOperation.Start) ||
         facts.AccessContext.WorkspaceType == WorkspaceType.Personal) &&
        !facts.OrganizationId.HasValue &&
        facts.ActorAgeCategory == CommunicationAgeCategory.Adult &&
        facts.TargetAgeCategory == CommunicationAgeCategory.Adult &&
        facts.HasActivePersonalGrant;

    private static bool CanUseOrganization(CommunicationEligibilityFacts facts)
    {
        if (!facts.OrganizationId.HasValue ||
            facts.AccessContext.WorkspaceType != WorkspaceType.Organization ||
            facts.AccessContext.OrganizationId != facts.OrganizationId ||
            !facts.AccessContext.ActiveRole.HasValue ||
            !facts.ActorMembershipActive ||
            !facts.ActorRoleActive ||
            !facts.TargetMembershipActive ||
            facts.TargetActiveRoles.Count == 0)
        {
            return false;
        }

        return facts.AccessContext.ActiveRole.Value switch
        {
            OrganizationRole.Manager =>
                HasRole(facts, OrganizationRole.Manager) ||
                HasRole(facts, OrganizationRole.Teacher) ||
                HasRole(facts, OrganizationRole.Parent) ||
                HasRole(facts, OrganizationRole.Student) && facts.TargetHasActiveEnrollment,

            OrganizationRole.Teacher =>
                HasRole(facts, OrganizationRole.Manager) ||
                HasRole(facts, OrganizationRole.Teacher) ||
                HasRole(facts, OrganizationRole.Student) &&
                facts.TargetHasActiveEnrollment &&
                facts.HasActiveTeacherStudentRelationship ||
                HasRole(facts, OrganizationRole.Parent) &&
                facts.HasActiveTeacherParentRelationship,

            OrganizationRole.Student =>
                facts.ActorHasActiveEnrollment &&
                (HasRole(facts, OrganizationRole.Manager) ||
                 HasRole(facts, OrganizationRole.Teacher) &&
                 facts.HasActiveTeacherStudentRelationship),

            OrganizationRole.Parent =>
                HasRole(facts, OrganizationRole.Manager) ||
                HasRole(facts, OrganizationRole.Teacher) &&
                facts.HasActiveTeacherParentRelationship,

            _ => false
        };
    }

    private static bool HasRole(
        CommunicationEligibilityFacts facts,
        OrganizationRole role) => facts.TargetActiveRoles.Contains(role);
}
