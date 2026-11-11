namespace Laborator8_PSSC.Events;

// Port through which an integration event is published on a topic.
public interface IEventSender
{
    Task SendAsync<TEvent>(TopicName topic, TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent;
}
