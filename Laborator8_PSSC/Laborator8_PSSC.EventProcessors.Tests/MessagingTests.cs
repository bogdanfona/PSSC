using System.Text.Json;
using Laborator8_PSSC.Contracts.Events;
using Laborator8_PSSC.Events;
using Laborator8_PSSC.Events.ServiceBus;
using Laborator8_PSSC.Invoicing.EventProcessor;
using Microsoft.Extensions.DependencyInjection;

namespace Laborator8_PSSC.EventProcessors.Tests;

// The transport-side rules that do not need a broker: routing name, decoding, redelivery spacing, mapping.
public class MessagingTests
{
    [Fact]
    public void OrderPlacedEvent_HasAStableVersionedType() =>
        Assert.Equal("pssc.orders.placed.v1", OrderPlacedIntegrationEvent.EventType);

    [Fact]
    public async Task Dispatch_ValidPayload_CallsTheHandlerWithTheEvent()
    {
        var recorder = new RecordingHandler();

        var result = await Dispatcher(recorder).DispatchAsync(Payload("""{"orderId":5,"customerCode":"C-1","customerName":"Ana","deliveryAddress":"Str. 1","lines":[],"total":0,"placedAt":"2026-11-11T10:00:00"}"""), CancellationToken.None);

        Assert.Equal(EventProcessingResult.Completed, result);
        Assert.Equal(5, recorder.Received!.OrderId);
    }

    [Fact]
    public async Task Dispatch_DataOfTheWrongShape_FailsWithoutCallingTheHandler()
    {
        var recorder = new RecordingHandler();

        var result = await Dispatcher(recorder).DispatchAsync(Payload("""{"orderId":"not a number"}"""), CancellationToken.None);

        Assert.Equal(EventProcessingResult.Failed, result);
        Assert.Null(recorder.Received);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(6, 30)]
    public void RetryDelay_DoublesWithEachDeliveryUpToThirtySeconds(int deliveryCount, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), ServiceBusTopicEventListener.RetryDelay(deliveryCount));

    [Fact]
    public void ToCommand_KeepsEveryLineAndTheTotal()
    {
        var e = new OrderPlacedIntegrationEvent(9, "C-9", "Ion", "Str. 9", [new OrderPlacedLine("P-1", "Paine", 2, 3.80m)], 7.60m, new DateTime(2026, 11, 11));

        var command = e.ToCommand();

        Assert.Equal(9, command.OrderId);
        Assert.Equal("Str. 9", command.BillingAddress);
        Assert.Equal(7.60m, command.OrderTotal);
        Assert.Equal(3.80m, Assert.Single(command.Lines).UnitPrice);
    }

    private static JsonElement Payload(string json) => JsonDocument.Parse(json).RootElement;

    private static EventDispatcher<OrderPlacedIntegrationEvent> Dispatcher(RecordingHandler recorder)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<OrderPlacedIntegrationEvent>>(recorder);
        return new EventDispatcher<OrderPlacedIntegrationEvent>(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private sealed class RecordingHandler : IEventHandler<OrderPlacedIntegrationEvent>
    {
        public OrderPlacedIntegrationEvent? Received { get; private set; }

        public Task<EventProcessingResult> HandleAsync(OrderPlacedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            Received = integrationEvent;
            return Task.FromResult(EventProcessingResult.Completed);
        }
    }
}
