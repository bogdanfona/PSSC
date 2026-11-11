using System.Collections.Frozen;
using System.Net.Mime;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;
using Microsoft.Extensions.Logging;

namespace Laborator8_PSSC.Events.ServiceBus;

// Listens to a Service Bus subscription, decodes each CloudEvent, routes it by its EventType and settles it
// from the handler's result. Nothing is ignored: a message is completed, delivered again, or dead-lettered.
internal sealed partial class ServiceBusTopicEventListener(
    ServiceBusClient client,
    IEnumerable<IEventDispatcher> dispatchers,
    ILogger<ServiceBusTopicEventListener> logger) : IEventListener
{
    private static readonly JsonEventFormatter Formatter = new();

    // Throws at startup if two handlers declare the same EventType: a configuration error found before the first message.
    private readonly FrozenDictionary<string, IEventDispatcher> routes =
        dispatchers.ToFrozenDictionary(d => d.EventType, StringComparer.Ordinal);

    private readonly SemaphoreSlim gate = new(1, 1);
    private ServiceBusProcessor? processor;

    public async Task StartAsync(TopicName topic, SubscriptionName subscription, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (processor is not null)
                return;

            processor = client.CreateProcessor(topic.Value, subscription.Value, new ServiceBusProcessorOptions
            {
                // Each message is settled explicitly, from the handler's result.
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 2,
            });
            processor.ProcessMessageAsync += OnMessageAsync;
            processor.ProcessErrorAsync += OnProcessorErrorAsync;
            await processor.StartProcessingAsync(cancellationToken);
            LogStarted(logger, topic.Value, subscription.Value);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (processor is { IsProcessing: true })
            await processor.StopProcessingAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (processor is not null)
        {
            await processor.DisposeAsync();
            processor = null;
        }
        gate.Dispose();
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        var message = args.Message;
        var cancellationToken = args.CancellationToken;

        CloudEvent cloudEvent;
        try
        {
            var contentType = message.ContentType is null ? null : new ContentType(message.ContentType);
            cloudEvent = Formatter.DecodeStructuredModeMessage(message.Body.ToStream(), contentType, extensionAttributes: null);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or JsonException)
        {
            LogUndecodable(logger, message.MessageId, ex.Message);
            await args.DeadLetterMessageAsync(message, "InvalidCloudEvent", ex.Message, cancellationToken);
            return;
        }

        if (cloudEvent.Type is null || !routes.TryGetValue(cloudEvent.Type, out var dispatcher) || cloudEvent.Data is not JsonElement data)
        {
            LogNoHandler(logger, cloudEvent.Type, message.MessageId);
            await args.DeadLetterMessageAsync(message, "NoHandler", $"No handler registered for '{cloudEvent.Type}'.", cancellationToken);
            return;
        }

        // No catch-all here: if the handler throws, the processor abandons the message and it is delivered again;
        // after MaxDeliveryCount deliveries the broker moves it to the dead-letter queue.
        var result = await dispatcher.DispatchAsync(data, cancellationToken);
        await SettleAsync(args, message, result, cancellationToken);
        LogSettled(logger, cloudEvent.Type, message.MessageId, result, message.DeliveryCount);
    }

    private static async Task SettleAsync(ProcessMessageEventArgs args, ServiceBusReceivedMessage message, EventProcessingResult result, CancellationToken cancellationToken)
    {
        switch (result)
        {
            case EventProcessingResult.Completed:
                await args.CompleteMessageAsync(message, cancellationToken);
                break;
            case EventProcessingResult.Retry:
                // An abandoned message comes back at once, so MaxDeliveryCount (5) would be spent in seconds.
                // Waiting first (2, 4, 8, 16 s) spreads the deliveries over about a minute, while the message
                // stays locked (LockDuration is 1 minute), giving a dependency time to come back.
                await Task.Delay(RetryDelay(message.DeliveryCount), cancellationToken);
                await args.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                break;
            default:
                await args.DeadLetterMessageAsync(message, "HandlerFailed", "The handler returned Failed.", cancellationToken);
                break;
        }
    }

    internal static TimeSpan RetryDelay(int deliveryCount) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Math.Max(deliveryCount, 1)), 30));

    private Task OnProcessorErrorAsync(ProcessErrorEventArgs args)
    {
        LogProcessorError(logger, args.ErrorSource, args.EntityPath, args.Exception);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Listening on topic {Topic}, subscription {Subscription}")]
    private static partial void LogStarted(ILogger logger, string topic, string subscription);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Message {MessageId} is not a valid CloudEvent ({Reason}); dead-lettered")]
    private static partial void LogUndecodable(ILogger logger, string messageId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No handler for event type {EventType} (message {MessageId}); dead-lettered")]
    private static partial void LogNoHandler(ILogger logger, string? eventType, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Event {EventType} (message {MessageId}) settled as {Result} on delivery {DeliveryCount}")]
    private static partial void LogSettled(ILogger logger, string eventType, string messageId, EventProcessingResult result, int deliveryCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Service Bus processor error: source={ErrorSource}, entity={EntityPath}")]
    private static partial void LogProcessorError(ILogger logger, ServiceBusErrorSource errorSource, string entityPath, Exception exception);
}
