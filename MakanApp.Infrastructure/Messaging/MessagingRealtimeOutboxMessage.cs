using MakanApp.Domain.Messaging;

namespace MakanApp.Infrastructure.Messaging;

public sealed class MessagingRealtimeOutboxMessage
{
    private MessagingRealtimeOutboxMessage()
    {
    }

    private MessagingRealtimeOutboxMessage(
        Guid id,
        Guid changeEventId,
        DateTime occurredAtUtc)
    {
        Id = id;
        ChangeEventId = changeEventId;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ChangeEventId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime? ClaimedUntilUtc { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }
    public DateTime? DispatchedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static MessagingRealtimeOutboxMessage Create(
        MessagingChangeEvent change,
        DateTime occurredAtUtc)
    {
        if (change.Id == Guid.Empty)
        {
            throw new ArgumentException("شناسه تغییر برای Outbox الزامی است.", nameof(change));
        }

        ValidateUtc(occurredAtUtc, nameof(occurredAtUtc));
        return new MessagingRealtimeOutboxMessage(Guid.NewGuid(), change.Id, occurredAtUtc);
    }

    public void Claim(DateTime nowUtc, DateTime claimedUntilUtc)
    {
        ValidateUtc(nowUtc, nameof(nowUtc));
        ValidateUtc(claimedUntilUtc, nameof(claimedUntilUtc));
        if (claimedUntilUtc <= nowUtc || DispatchedAtUtc.HasValue)
        {
            throw new InvalidOperationException("Outbox در وضعیت قابل claim نیست.");
        }

        ClaimedUntilUtc = claimedUntilUtc;
        NextAttemptAtUtc = null;
        AttemptCount++;
    }

    public void MarkDispatched(DateTime dispatchedAtUtc)
    {
        ValidateUtc(dispatchedAtUtc, nameof(dispatchedAtUtc));
        DispatchedAtUtc = dispatchedAtUtc;
        ClaimedUntilUtc = null;
        NextAttemptAtUtc = null;
    }

    public void ScheduleRetry(DateTime nextAttemptAtUtc)
    {
        ValidateUtc(nextAttemptAtUtc, nameof(nextAttemptAtUtc));
        if (DispatchedAtUtc.HasValue)
        {
            return;
        }

        ClaimedUntilUtc = null;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }

    private static void ValidateUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("زمان Outbox باید UTC باشد.", parameterName);
        }
    }
}
