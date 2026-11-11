using System.Collections.Concurrent;
using System.Net.Mime;
using Azure.Messaging.ServiceBus;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Laborator8_PSSC.Events.ServiceBus;

// Sends integration events as CloudEvents (structured mode, JSON) on an Azure Service Bus topic.
internal sealed partial class ServiceBusTopicEventSender(
    ServiceBusClient client,
    TimeProvider clock,
    IOptions<ServiceBusEventsOptions> options,
    ILogger<ServiceBusTopicEventSender> logger) : IEventSender, IAsyncDisposable
{
    private static readonly JsonEventFormatter Formatter = new();
    private readonly ConcurrentDictionary<TopicName, ServiceBusSender> senders = new();

    public async Task SendAsync<TEvent>(TopicName topic, TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var cloudEvent = new CloudEvent
        {
            Id = Guid.NewGuid().ToString(),
            Type = TEvent.EventType,
            Source = options.Value.Source,
            Time = clock.GetUtcNow(),
            Subject = topic.Value,
            DataContentType = MediaTypeNames.Application.Json,
            Data = integrationEvent,
        };

        var body = Formatter.EncodeStructuredModeMessage(cloudEvent, out var contentType);
        var message = new ServiceBusMessage(body)
        {
            MessageId = cloudEvent.Id,
            ContentType = contentType.ToString(),
            Subject = TEvent.EventType,
        };

        // One sender per topic, kept for the lifetime of the service.
        var sender = senders.GetOrAdd(topic, t => client.CreateSender(t.Value));
        await sender.SendMessageAsync(message, cancellationToken);
        LogEventSent(logger, TEvent.EventType, topic.Value, cloudEvent.Id);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in senders.Values)
            await sender.DisposeAsync();
        senders.Clear();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Event {EventType} sent to topic {Topic} (id {EventId})")]
    private static partial void LogEventSent(ILogger logger, string eventType, string topic, string eventId);
}
