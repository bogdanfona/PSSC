namespace Laborator8_PSSC.Events;

// An integration event: data sent to another bounded context through asynchronous messaging.
// EventType is a static abstract member: each event declares its stable, versioned routing name at compile time
// (for example "pssc.orders.placed.v1"), instead of the C# class name, which could change without warning consumers.
public interface IIntegrationEvent
{
    static abstract string EventType { get; }
}
