namespace MakanApp.Domain.Messaging;

public sealed class ConversationParticipant
{
    private ConversationParticipant()
    {
    }

    private ConversationParticipant(
        Guid id,
        Guid conversationId,
        Guid userId,
        ConversationParticipantRole role,
        DateTime joinedAtUtc)
    {
        Id = id;
        ConversationId = conversationId;
        UserId = userId;
        Role = role;
        Status = ConversationParticipantStatus.Active;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public ConversationParticipantRole Role { get; private set; }
    public ConversationParticipantStatus Status { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public DateTime? EndedAtUtc { get; private set; }
    public Guid? EndedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == ConversationParticipantStatus.Active && !EndedAtUtc.HasValue;

    public static ConversationParticipant CreateActive(
        Guid conversationId,
        Guid userId,
        DateTime joinedAtUtc,
        ConversationParticipantRole role = ConversationParticipantRole.Member)
    {
        if (conversationId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("شناسه گفتگو و کاربر الزامی است.");
        }

        if (joinedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان عضویت باید UTC باشد.", nameof(joinedAtUtc));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        return new ConversationParticipant(Guid.NewGuid(), conversationId, userId, role, joinedAtUtc);
    }

    public void Remove(Guid endedByUserId, DateTime endedAtUtc)
    {
        if (endedByUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه اقدام‌کننده الزامی است.", nameof(endedByUserId));
        }

        End(ConversationParticipantStatus.Removed, endedByUserId, endedAtUtc);
    }

    public void Leave(DateTime endedAtUtc) =>
        End(ConversationParticipantStatus.Left, UserId, endedAtUtc);

    public void ChangeRole(ConversationParticipantRole role)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("نقش عضویت پایان‌یافته قابل تغییر نیست.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        Role = role;
    }

    private void End(
        ConversationParticipantStatus status,
        Guid endedByUserId,
        DateTime endedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        if (status is not ConversationParticipantStatus.Removed and not ConversationParticipantStatus.Left)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (endedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان پایان عضویت باید UTC باشد.", nameof(endedAtUtc));
        }

        Status = status;
        EndedAtUtc = endedAtUtc;
        EndedByUserId = endedByUserId;
    }
}
