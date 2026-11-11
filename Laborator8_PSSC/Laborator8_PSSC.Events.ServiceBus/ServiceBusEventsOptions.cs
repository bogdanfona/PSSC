namespace Laborator8_PSSC.Events.ServiceBus;

// Settings for sending events, bound from the "ServiceBusEvents" section.
public sealed class ServiceBusEventsOptions
{
    public const string SectionName = "ServiceBusEvents";

    // CloudEvent.Source of every event sent by this service.
    public Uri Source { get; set; } = new("urn:pssc:laborator8-api");
}
