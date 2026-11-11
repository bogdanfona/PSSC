using Laborator8_PSSC.Contracts.Events;
using Laborator8_PSSC.Data.Queries;

namespace Laborator8_PSSC.Api.Mapping;

// From the paid cart, as stored, to the integration event the other contexts understand.
public static class OrderPlacedMapping
{
    public static OrderPlacedIntegrationEvent ToOrderPlacedEvent(this CartView cart) => new(
        cart.Id,
        cart.CustomerCode,
        cart.CustomerName,
        cart.DeliveryAddress,
        [.. cart.Lines.Select(l => new OrderPlacedLine(l.ProductCode, l.ProductName, l.Quantity, l.UnitPrice))],
        cart.Total,
        cart.PaidAt ?? throw new InvalidOperationException($"Cosul {cart.Id} nu este platit."));
}
