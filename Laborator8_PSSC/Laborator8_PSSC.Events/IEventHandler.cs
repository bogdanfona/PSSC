namespace Laborator8_PSSC.Events;

// Handles one type of integration event. It receives a plain record, never a transport message,
// and says how the message should be settled. Delivery is at-least-once: the same event can arrive
// more than once, so handling it again must not change the outcome (idempotency).
public interface IEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task<EventProcessingResult> HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
