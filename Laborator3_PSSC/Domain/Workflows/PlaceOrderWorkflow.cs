using Laborator3_PSSC.Domain.Models;
using Laborator3_PSSC.Domain.Operations;

namespace Laborator3_PSSC.Domain.Workflows;

public sealed class PlaceOrderWorkflow(
    CheckProductExists productExists,
    GetStock getStock,
    CheckAddressDeliverable isDeliverable,
    GetProductPrice getPrice)
{
    // Each step runs only if the previous one succeeded; the first failure becomes the result.
    public Result<OrderPlacedEvent, PlaceOrderError> Execute(UnvalidatedOrder order) =>
        ValidateOrderOperation.Execute(order)
            .Bind(validated => CheckProductsExistOperation.Execute(validated, productExists))
            .Bind(validated => CheckStockOperation.Execute(validated, getStock))
            .Bind(validated => CheckShippingAddressOperation.Execute(validated, isDeliverable))
            .Map(validated => CalculatePriceOperation.Execute(validated, getPrice))
            .Map(priced => new OrderPlacedEvent(
                OrderId.New(),
                priced.CustomerName,
                priced.ShippingAddress,
                priced.Lines,
                priced.Total,
                DateTime.Now));
}
