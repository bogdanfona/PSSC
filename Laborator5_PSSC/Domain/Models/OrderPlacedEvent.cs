namespace Laborator5_PSSC.Domain.Models;

// A fact: the order was placed. It gets its OrderId only when it is saved to the database.
public sealed record OrderPlacedEvent(
    CustomerCode CustomerCode,
    CustomerName CustomerName,
    Address ShippingAddress,
    IReadOnlyList<PricedOrderLine> Lines,
    decimal Total,
    DateTime PlacedAt);
