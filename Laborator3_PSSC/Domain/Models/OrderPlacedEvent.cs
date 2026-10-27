namespace Laborator3_PSSC.Domain.Models;

// A fact: the order was placed.
public sealed record OrderPlacedEvent(
    OrderId OrderId,
    CustomerName CustomerName,
    Address ShippingAddress,
    IReadOnlyList<PricedOrderLine> Lines,
    decimal Total,
    DateTime PlacedAt);
