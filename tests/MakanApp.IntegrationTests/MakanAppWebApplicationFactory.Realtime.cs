using MakanApp.Application.Messaging;

namespace MakanApp.IntegrationTests;

public sealed class RealtimeFailureSwitch
{
    private TaskCompletionSource _failureObserved = NewCompletionSource();

    public bool FailDispatch { get; set; }

    public Task FailureObserved => _failureObserved.Task;

    public void RecordFailure() => _failureObserved.TrySetResult();

    public void Reset()
    {
        FailDispatch = false;
        _failureObserved = NewCompletionSource();
    }

    private static TaskCompletionSource NewCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class FaultInjectingMessagingRealtimeNotifier(
    IMessagingRealtimeNotifier inner,
    RealtimeFailureSwitch failureSwitch) : IMessagingRealtimeNotifier
{
    public Task NotifyConversationChangedAsync(
        MessagingRealtimeNotification notification,
        Guid? audienceUserId,
        CancellationToken cancellationToken)
    {
        if (failureSwitch.FailDispatch)
        {
            failureSwitch.RecordFailure();
            throw new InvalidOperationException("Injected realtime dispatch failure.");
        }

        return inner.NotifyConversationChangedAsync(notification, audienceUserId, cancellationToken);
    }
}
