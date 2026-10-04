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
        DateTime joinedAtUtc)
    {
        Id = id;
        ConversationId = conversationId;
        UserId = userId;
        Status = ConversationParticipantStatus.Active;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid UserId { get; private set; }
    public ConversationParticipantStatus Status { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public DateTime? LeftAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsActive => Status == ConversationParticipantStatus.Active && !LeftAtUtc.HasValue;

    public static ConversationParticipant CreateActive(
        Guid conversationId,
        Guid userId,
        DateTime joinedAtUtc)
    {
        if (conversationId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("شناسه گفتگو و کاربر الزامی است.");
        }

        if (joinedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان عضویت باید UTC باشد.", nameof(joinedAtUtc));
        }

        return new ConversationParticipant(Guid.NewGuid(), conversationId, userId, joinedAtUtc);
    }

    public void Revoke(DateTime revokedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }

        Status = ConversationParticipantStatus.Revoked;
        LeftAtUtc = revokedAtUtc;
    }
}
