using Laborator5_PSSC.Domain.Models;
using Laborator5_PSSC.Domain.Operations;
using Laborator5_PSSC.Domain.Repositories;
using Laborator5_PSSC.Functional;

namespace Laborator5_PSSC.Domain.Workflows;

// Impure -> pure -> impure: read the state it needs, decide with a pure function, save on success.
// Exceptions are not caught: they are infrastructure errors, not order errors, and belong to the host.
public sealed class PlaceOrderWorkflow(
    IProductsRepository productsRepository,
    IOrdersRepository ordersRepository,
    CheckAddressDeliverable isDeliverable,
    TimeProvider clock)
{
    public async Task<Result<SavedOrder, PlaceOrderError>> ExecuteAsync(UnvalidatedOrder order, CancellationToken cancellationToken)
    {
        // Only the products named in the order are loaded.
        var catalog = await productsRepository.GetByCodesAsync(order.ProductCodes, cancellationToken);

        var placed = Place(order, catalog, isDeliverable, clock.GetLocalNow().DateTime);

        return placed switch
        {
            Result<OrderPlacedEvent, PlaceOrderError>.Success s => (await ordersRepository.SaveAsync(s.Value, cancellationToken))
                .Map(id => new SavedOrder(id, s.Value)),
            Result<OrderPlacedEvent, PlaceOrderError>.Failure f => new Result<SavedOrder, PlaceOrderError>.Failure(f.Error),
            _ => throw new InvalidOperationException()
        };
    }

    // The pure core: every input is a parameter, so it is tested without repositories or a database.
    // Each step runs only if the previous one succeeded; the first failure becomes the result.
    public static Result<OrderPlacedEvent, PlaceOrderError> Place(
        UnvalidatedOrder order, ProductCatalog catalog, CheckAddressDeliverable isDeliverable, DateTime now) =>
        ValidateOrderOperation.Execute(order)
            .Bind(validated => CheckProductsExistOperation.Execute(validated, catalog.Exists))
            .Bind(validated => CheckStockOperation.Execute(validated, catalog.GetStock))
            .Bind(validated => CheckShippingAddressOperation.Execute(validated, isDeliverable))
            .Map(validated => CalculatePriceOperation.Execute(validated, catalog.GetPrice))
            .Map(priced => new OrderPlacedEvent(
                priced.CustomerCode,
                priced.CustomerName,
                priced.ShippingAddress,
                priced.Lines,
                priced.Total,
                now));
}
