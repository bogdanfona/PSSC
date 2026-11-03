using Laborator5_PSSC.Domain.Models;
using Laborator5_PSSC.Domain.Operations;

namespace Laborator5_PSSC.Domain.Workflows;

public sealed class PlaceOrderWorkflow(
    CheckProductExists productExists,
    GetStock getStock,
    CheckAddressDeliverable isDeliverable,
    GetProductPrice getPrice)
{
    // Pure: all the state it needs comes through the injected functions, loaded before it runs.
    // Each step runs only if the previous one succeeded; the first failure becomes the result.
    public Result<OrderPlacedEvent, PlaceOrderError> Execute(UnvalidatedOrder order) =>
        ValidateOrderOperation.Execute(order)
            .Bind(validated => CheckProductsExistOperation.Execute(validated, productExists))
            .Bind(validated => CheckStockOperation.Execute(validated, getStock))
            .Bind(validated => CheckShippingAddressOperation.Execute(validated, isDeliverable))
            .Map(validated => CalculatePriceOperation.Execute(validated, getPrice))
            .Map(priced => new OrderPlacedEvent(
                priced.CustomerCode,
                priced.CustomerName,
                priced.ShippingAddress,
                priced.Lines,
                priced.Total,
                DateTime.Now));
}
