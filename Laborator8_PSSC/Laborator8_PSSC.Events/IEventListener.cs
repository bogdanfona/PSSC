namespace Laborator8_PSSC.Events;

// Listens to a subscription and hands every message to the handler registered for its event type.
// StartAsync is idempotent: a second call does not start a second listener.
public interface IEventListener : IAsyncDisposable
{
    Task StartAsync(TopicName topic, SubscriptionName subscription, CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
