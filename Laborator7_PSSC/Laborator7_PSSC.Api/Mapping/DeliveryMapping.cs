using Laborator7_PSSC.Contracts.Deliveries;
using Laborator7_PSSC.Data.Queries;

namespace Laborator7_PSSC.Api.Mapping;

// From the paid cart, as stored, to the contract the delivery context understands.
public static class DeliveryMapping
{
    public static DeliveryRequest ToDeliveryRequest(this CartView cart) => new(
        cart.Id,
        cart.CustomerCode,
        cart.CustomerName,
        cart.DeliveryAddress,
        [.. cart.Lines.Select(l => new DeliveryItem(l.ProductCode, l.ProductName, l.Quantity))],
        cart.Total,
        cart.PaidAt ?? throw new InvalidOperationException($"Cosul {cart.Id} nu este platit."));
}
