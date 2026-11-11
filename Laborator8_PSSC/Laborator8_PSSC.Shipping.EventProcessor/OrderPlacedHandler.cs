using System.Net;
using Laborator8_PSSC.Contracts.Deliveries;
using Laborator8_PSSC.Contracts.Events;
using Laborator8_PSSC.Events;
using Laborator8_PSSC.Shipping.EventProcessor.Clients;
using Microsoft.Extensions.Logging;
using Polly.Timeout;

namespace Laborator8_PSSC.Shipping.EventProcessor;

// "Order placed" in the shipping context: starts the delivery by calling the delivery API (Lab 7).
// Two levels of retry: the HTTP client retries 3 times within seconds; if the API is still down, the message is
// abandoned and Service Bus delivers it again later (up to MaxDeliveryCount, then the dead-letter queue).
// Repeating is safe: the delivery API keeps one delivery per order and returns the existing one.
public sealed partial class OrderPlacedHandler(DeliveryApiClient deliveryApi, ILogger<OrderPlacedHandler> logger)
    : IEventHandler<OrderPlacedIntegrationEvent>
{
    public async Task<EventProcessingResult> HandleAsync(OrderPlacedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        try
        {
            var delivery = await deliveryApi.ScheduleAsync(integrationEvent.ToDeliveryRequest(), cancellationToken);
            LogScheduled(logger, delivery.TrackingNumber, integrationEvent.OrderId, delivery.EstimatedDeliveryDate);
            return EventProcessingResult.Completed;
        }
        // The delivery API rejected the request (400): sending it again would give the same answer.
        catch (HttpRequestException ex) when (IsPermanent(ex.StatusCode))
        {
            LogRejected(logger, ex, integrationEvent.OrderId, (int?)ex.StatusCode);
            return EventProcessingResult.Failed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException && !cancellationToken.IsCancellationRequested)
        {
            LogUnavailable(logger, integrationEvent.OrderId);
            return EventProcessingResult.Retry;
        }
    }

    // 4xx means the request itself is wrong, except 408 and 429, which are transient.
    private static bool IsPermanent(HttpStatusCode? status) =>
        status is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError
        and not HttpStatusCode.RequestTimeout and not HttpStatusCode.TooManyRequests;

    [LoggerMessage(Level = LogLevel.Information, Message = "Delivery {TrackingNumber} started for order {OrderId}, expected on {EstimatedDate}")]
    private static partial void LogScheduled(ILogger logger, string trackingNumber, int orderId, DateOnly estimatedDate);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The delivery API rejected order {OrderId} with status {Status}")]
    private static partial void LogRejected(ILogger logger, Exception exception, int orderId, int? status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The delivery API is unavailable for order {OrderId}; the message will be delivered again")]
    private static partial void LogUnavailable(ILogger logger, int orderId);
}

public static class OrderPlacedMapping
{
    public static DeliveryRequest ToDeliveryRequest(this OrderPlacedIntegrationEvent e) => new(
        e.OrderId,
        e.CustomerCode,
        e.CustomerName,
        e.DeliveryAddress,
        [.. (e.Lines ?? []).Select(l => new DeliveryItem(l.ProductCode, l.ProductName, l.Quantity))],
        e.Total,
        e.PlacedAt);
}
