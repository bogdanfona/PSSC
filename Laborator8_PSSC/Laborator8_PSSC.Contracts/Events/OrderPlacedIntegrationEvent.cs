using Laborator8_PSSC.Events;

namespace Laborator8_PSSC.Contracts.Events;

// Published by the cart API on the "order-placed" topic once an order has been taken (its cart was paid).
// Consumed independently by invoicing and shipping. Unit prices include VAT, as shown in the shop.
// Separate from the domain's events: it carries plain data only, so it can travel to other contexts.
public sealed record OrderPlacedIntegrationEvent(
    int OrderId,
    string CustomerCode,
    string CustomerName,
    string DeliveryAddress,
    IReadOnlyList<OrderPlacedLine> Lines,
    decimal Total,
    DateTime PlacedAt) : IIntegrationEvent
{
    public static string EventType => "pssc.orders.placed.v1";
}

public sealed record OrderPlacedLine(string ProductCode, string ProductName, decimal Quantity, decimal UnitPrice);
