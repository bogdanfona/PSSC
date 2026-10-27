using Laborator3_PSSC.Domain.Models;

namespace Laborator3_PSSC.Domain.Operations;

public static class CalculatePriceOperation
{
    public static PricedOrder Execute(ValidatedOrder order, GetProductPrice getPrice) =>
        new(order.CustomerName,
            order.ShippingAddress,
            order.Lines.Select(l => new PricedOrderLine(l.ProductCode, l.Quantity, getPrice(l.ProductCode))).ToList());
}
