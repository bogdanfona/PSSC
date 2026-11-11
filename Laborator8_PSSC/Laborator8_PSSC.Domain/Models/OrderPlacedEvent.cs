namespace Laborator8_PSSC.Domain.Models;

// A fact: the order was placed. It gets its OrderId only when it is saved to the database.
public sealed record OrderPlacedEvent(
    CustomerCode CustomerCode,
    CustomerName CustomerName,
    Address ShippingAddress,
    IReadOnlyList<PricedOrderLine> Lines,
    decimal Total,
    DateTime PlacedAt);

// The placed order after it was saved, with the id given by the database.
public sealed record SavedOrder(OrderId OrderId, OrderPlacedEvent Order);
