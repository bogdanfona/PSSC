using Laborator5_PSSC.Domain;
using Laborator5_PSSC.Domain.Models;
using Laborator5_PSSC.Domain.Repositories;
using Laborator5_PSSC.Domain.Workflows;

namespace Laborator5_PSSC.Application;

public sealed record SavedOrder(OrderId OrderId, OrderPlacedEvent Order);

// I/O stays at the edges: read state from the database -> pure workflow -> write the result.
public sealed class PlaceOrderService(
    IProductsRepository productsRepository,
    IOrdersRepository ordersRepository,
    CheckAddressDeliverable isDeliverable)
{
    public async Task<Result<SavedOrder, PlaceOrderError>> ExecuteAsync(UnvalidatedOrder input)
    {
        // 1. Load only the products named in the order. Invalid codes are skipped here;
        //    the workflow's validation step reports them.
        var codes = input.Lines
            .Select(l => ProductCode.Create(l.ProductCode))
            .OfType<Result<ProductCode, string>.Success>()
            .Select(s => s.Value)
            .Distinct();
        var catalog = await productsRepository.GetByCodesAsync(codes);

        // 2. Pure decision on the loaded state.
        var workflow = new PlaceOrderWorkflow(catalog.Exists, catalog.GetStock, isDeliverable, catalog.GetPrice);
        var result = workflow.Execute(input);

        // 3. Save only an order that was placed.
        return result switch
        {
            Result<OrderPlacedEvent, PlaceOrderError>.Success s => (await ordersRepository.SaveAsync(s.Value))
                .Map(id => new SavedOrder(id, s.Value)),
            Result<OrderPlacedEvent, PlaceOrderError>.Failure f => new Result<SavedOrder, PlaceOrderError>.Failure(f.Error),
            _ => throw new InvalidOperationException()
        };
    }
}
