using MakanApp.Domain.Messaging;

namespace MakanApp.Application.Messaging;

public sealed record CreateManagedConversationCommand(
    Guid ClientOperationId,
    ConversationScope Scope,
    string Title,
    string? Description,
    IReadOnlyCollection<Guid>? InitialParticipantUserIds);

public sealed record AddConversationMemberCommand(Guid UserId);

public sealed record ChangeConversationMemberRoleCommand(ConversationParticipantRole Role);

public sealed record StartOwnershipTransferCommand(Guid TargetUserId);

public sealed record ConversationParticipantResult(
    Guid ParticipantId,
    SafeMessagingIdentityResult User,
    ConversationParticipantRole Role,
    ConversationParticipantStatus Status,
    DateTime JoinedAtUtc,
    DateTime? EndedAtUtc);

public sealed record ManagedConversationResult(
    Guid ConversationId,
    ConversationType Type,
    ConversationScope Scope,
    Guid? OrganizationId,
    string Title,
    string? Description,
    ConversationManagementPolicy ManagementPolicy,
    ConversationStatus Status,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc,
    IReadOnlyCollection<ConversationParticipantResult> Participants,
    bool AlreadyExisted);

public sealed record OwnershipTransferResult(
    Guid TransferId,
    Guid ConversationId,
    Guid FromUserId,
    Guid ToUserId,
    OwnershipTransferStatus Status,
    DateTime CreatedAtUtc,
    DateTime? AcceptedAtUtc,
    DateTime? DeclinedAtUtc,
    bool AlreadyCompleted);
