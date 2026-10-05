namespace MakanApp.Domain.Messaging;

public sealed class ConversationOwnershipTransfer
{
    private ConversationOwnershipTransfer()
    {
    }

    private ConversationOwnershipTransfer(
        Guid id,
        Guid conversationId,
        Guid fromParticipantId,
        Guid fromUserId,
        Guid toParticipantId,
        Guid toUserId,
        DateTime createdAtUtc)
    {
        Id = id;
        ConversationId = conversationId;
        FromParticipantId = fromParticipantId;
        FromUserId = fromUserId;
        ToParticipantId = toParticipantId;
        ToUserId = toUserId;
        Status = OwnershipTransferStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid FromParticipantId { get; private set; }
    public Guid FromUserId { get; private set; }
    public Guid ToParticipantId { get; private set; }
    public Guid ToUserId { get; private set; }
    public OwnershipTransferStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? DeclinedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsPending => Status == OwnershipTransferStatus.Pending;

    public static ConversationOwnershipTransfer CreatePending(
        Guid conversationId,
        Guid fromParticipantId,
        Guid fromUserId,
        Guid toParticipantId,
        Guid toUserId,
        DateTime createdAtUtc)
    {
        if (conversationId == Guid.Empty || fromParticipantId == Guid.Empty ||
            fromUserId == Guid.Empty || toParticipantId == Guid.Empty || toUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه‌های انتقال مالکیت الزامی هستند.");
        }

        if (fromParticipantId == toParticipantId || fromUserId == toUserId)
        {
            throw new ArgumentException("مالکیت باید به عضو دیگری منتقل شود.");
        }

        ValidateUtc(createdAtUtc, nameof(createdAtUtc));
        return new ConversationOwnershipTransfer(
            Guid.NewGuid(),
            conversationId,
            fromParticipantId,
            fromUserId,
            toParticipantId,
            toUserId,
            createdAtUtc);
    }

    public void Accept(DateTime acceptedAtUtc)
    {
        if (!IsPending)
        {
            throw new InvalidOperationException("انتقال مالکیت در انتظار نیست.");
        }

        ValidateUtc(acceptedAtUtc, nameof(acceptedAtUtc));
        Status = OwnershipTransferStatus.Accepted;
        AcceptedAtUtc = acceptedAtUtc;
    }

    public void Decline(DateTime declinedAtUtc)
    {
        if (!IsPending)
        {
            throw new InvalidOperationException("انتقال مالکیت در انتظار نیست.");
        }

        ValidateUtc(declinedAtUtc, nameof(declinedAtUtc));
        Status = OwnershipTransferStatus.Declined;
        DeclinedAtUtc = declinedAtUtc;
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان انتقال مالکیت باید UTC باشد.", parameterName);
        }
    }
}
