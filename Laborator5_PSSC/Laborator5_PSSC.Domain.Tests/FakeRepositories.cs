using Laborator5_PSSC.Domain.Models;
using Laborator5_PSSC.Domain.Repositories;
using Laborator5_PSSC.Functional;

namespace Laborator5_PSSC.Domain.Tests;

// In-memory stand-ins for the ports, so the workflow is tested without a database.
internal sealed class FakeProductsRepository(params ProductInfo[] products) : IProductsRepository
{
    public List<string> RequestedCodes { get; } = [];

    public Task<ProductCatalog> GetByCodesAsync(IReadOnlyCollection<ProductCode> codes, CancellationToken cancellationToken)
    {
        RequestedCodes.AddRange(codes.Select(c => c.Value));
        return Task.FromResult(new ProductCatalog(products.Where(p => codes.Contains(p.Code))));
    }
}

internal sealed class RecordingOrdersRepository : IOrdersRepository
{
    public List<OrderPlacedEvent> Saved { get; } = [];

    public Task<Result<OrderId, PlaceOrderError>> SaveAsync(OrderPlacedEvent placed, CancellationToken cancellationToken)
    {
        Saved.Add(placed);
        return Task.FromResult<Result<OrderId, PlaceOrderError>>(
            new Result<OrderId, PlaceOrderError>.Success(new OrderId(Saved.Count)));
    }
}
