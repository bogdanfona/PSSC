using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Laborator8_PSSC.Events.ServiceBus;

// Turns the data of a decoded CloudEvent into TEvent and hands it to its handler, in a new DI scope per message.
// Internal: the rest of the application only sees IEventHandler<TEvent>, never raw JSON.
internal interface IEventDispatcher
{
    string EventType { get; }

    Task<EventProcessingResult> DispatchAsync(JsonElement payload, CancellationToken cancellationToken);
}

internal sealed class EventDispatcher<TEvent>(IServiceScopeFactory scopeFactory) : IEventDispatcher
    where TEvent : IIntegrationEvent
{
    public string EventType => TEvent.EventType;

    public async Task<EventProcessingResult> DispatchAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        TEvent? integrationEvent;
        try
        {
            integrationEvent = payload.Deserialize<TEvent>(JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            integrationEvent = default;
        }

        // A valid CloudEvent whose data does not fit TEvent is corrupt, not a transient error: dead-letter it.
        if (integrationEvent is null)
            return EventProcessingResult.Failed;

        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IEventHandler<TEvent>>();
        return await handler.HandleAsync(integrationEvent, cancellationToken);
    }
}
