using System.Net;
using Laborator8_PSSC.Contracts.Events;
using Laborator8_PSSC.Events;
using Laborator8_PSSC.Shipping.EventProcessor;
using Microsoft.Extensions.Logging.Abstractions;
using static Laborator8_PSSC.EventProcessors.Tests.DeliveryApiClientRetryTests;

namespace Laborator8_PSSC.EventProcessors.Tests;

// How the shipping handler turns the delivery API's answer into the settlement of the message.
public class ShippingHandlerTests
{
    private static readonly OrderPlacedIntegrationEvent Event = new(
        7, "C-100", "Ana Pop", "Str. Lalelelor 5, Timisoara 300001",
        [new OrderPlacedLine("MER-0001", "Mere", 3, 4.20m)], 12.60m, new DateTime(2026, 11, 11, 12, 0, 0));

    [Fact]
    public async Task HandleAsync_DeliveryScheduled_CompletesTheMessage()
    {
        var handler = new ScriptedHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);

        var result = await Handler(handler).HandleAsync(Event, CancellationToken.None);

        Assert.Equal(EventProcessingResult.Completed, result);
        Assert.Equal(Event.OrderId, handler.LastBody!.CartId);
        Assert.Equal("MER-0001", Assert.Single(handler.LastBody.Items).ProductCode);
    }

    [Fact]
    public async Task HandleAsync_ApiDownAfterHttpRetries_AsksForRedelivery()
    {
        var handler = new ScriptedHandler(HttpStatusCode.ServiceUnavailable);

        var result = await Handler(handler).HandleAsync(Event, CancellationToken.None);

        Assert.Equal(EventProcessingResult.Retry, result);
        Assert.Equal(4, handler.Attempts);
    }

    [Fact]
    public async Task HandleAsync_NetworkError_AsksForRedelivery()
    {
        var result = await Handler(new ScriptedHandler((HttpStatusCode?)null)).HandleAsync(Event, CancellationToken.None);

        Assert.Equal(EventProcessingResult.Retry, result);
    }

    [Fact]
    public async Task HandleAsync_RequestRejected_DeadLettersTheMessage()
    {
        var handler = new ScriptedHandler(HttpStatusCode.BadRequest);

        var result = await Handler(handler).HandleAsync(Event, CancellationToken.None);

        Assert.Equal(EventProcessingResult.Failed, result);
        Assert.Equal(1, handler.Attempts);
    }

    private static OrderPlacedHandler Handler(ScriptedHandler http) =>
        new(CreateClient(http), NullLogger<OrderPlacedHandler>.Instance);
}
