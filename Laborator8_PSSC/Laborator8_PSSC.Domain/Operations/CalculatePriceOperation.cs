using Laborator8_PSSC.Domain.Models;

namespace Laborator8_PSSC.Domain.Operations;

public static class CalculatePriceOperation
{
    public static PricedOrder Execute(ValidatedOrder order, GetProductPrice getPrice) =>
        new(order.CustomerCode,
            order.CustomerName,
            order.ShippingAddress,
            order.Lines.Select(l => new PricedOrderLine(l.ProductCode, l.Quantity, getPrice(l.ProductCode))).ToList());
}
