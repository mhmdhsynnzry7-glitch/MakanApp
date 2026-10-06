namespace MakanApp.Domain.Messaging;

public sealed class MessagingChangeEvent
{
    public const int ResourceVersionMaximumLength = 128;
    public const int CurrentPayloadVersion = 1;

    private MessagingChangeEvent()
    {
    }

    private MessagingChangeEvent(
        Guid id,
        Guid conversationId,
        long changeSequence,
        MessagingChangeType changeType,
        Guid resourceId,
        string? resourceVersion,
        DateTime occurredAtUtc,
        Guid? actorUserId,
        Guid? audienceUserId)
    {
        Id = id;
        ConversationId = conversationId;
        ChangeSequence = changeSequence;
        ChangeType = changeType;
        ResourceId = resourceId;
        ResourceVersion = resourceVersion;
        OccurredAtUtc = occurredAtUtc;
        ActorUserId = actorUserId;
        AudienceUserId = audienceUserId;
        PayloadVersion = CurrentPayloadVersion;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public long ChangeSequence { get; private set; }
    public MessagingChangeType ChangeType { get; private set; }
    public Guid ResourceId { get; private set; }
    public string? ResourceVersion { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public Guid? AudienceUserId { get; private set; }
    public int PayloadVersion { get; private set; }

    public static MessagingChangeEvent Create(
        Guid conversationId,
        long changeSequence,
        MessagingChangeType changeType,
        Guid resourceId,
        string? resourceVersion,
        DateTime occurredAtUtc,
        Guid? actorUserId = null,
        Guid? audienceUserId = null)
    {
        if (conversationId == Guid.Empty || resourceId == Guid.Empty)
        {
            throw new ArgumentException("شناسه گفتگو و منبع تغییر الزامی است.");
        }

        if (changeSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changeSequence));
        }

        if (!Enum.IsDefined(changeType))
        {
            throw new ArgumentOutOfRangeException(nameof(changeType));
        }

        if (actorUserId == Guid.Empty || audienceUserId == Guid.Empty)
        {
            throw new ArgumentException("شناسه کاربر تغییر معتبر نیست.");
        }

        if (occurredAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان تغییر باید UTC باشد.", nameof(occurredAtUtc));
        }

        var normalizedVersion = string.IsNullOrWhiteSpace(resourceVersion)
            ? null
            : resourceVersion.Trim();
        if (normalizedVersion?.Length > ResourceVersionMaximumLength)
        {
            throw new ArgumentException("نسخه منبع تغییر بیش از طول مجاز است.", nameof(resourceVersion));
        }

        return new MessagingChangeEvent(
            Guid.NewGuid(),
            conversationId,
            changeSequence,
            changeType,
            resourceId,
            normalizedVersion,
            occurredAtUtc,
            actorUserId,
            audienceUserId);
    }
}
