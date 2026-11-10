using Laborator7_PSSC.Domain.Models;
using Laborator7_PSSC.Domain.Repositories;
using Laborator7_PSSC.Functional;

namespace Laborator7_PSSC.Domain.Tests;

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

internal sealed class FakeCartsRepository(params Cart[] carts) : ICartsRepository
{
    public List<ProductAddedToCartEvent> Added { get; } = [];
    public List<CartPaidEvent> Paid { get; } = [];

    public Task<Cart?> GetAsync(OrderId id, CancellationToken cancellationToken) =>
        Task.FromResult(carts.FirstOrDefault(c => c.Id == id));

    public Task<Result<ProductAddedToCartEvent, AddProductToCartError>> AddProductAsync(
        ProductAddedToCartEvent added, CancellationToken cancellationToken)
    {
        Added.Add(added);
        return Task.FromResult<Result<ProductAddedToCartEvent, AddProductToCartError>>(
            new Result<ProductAddedToCartEvent, AddProductToCartError>.Success(added));
    }

    public Task<Result<CartPaidEvent, PayCartError>> MarkPaidAsync(CartPaidEvent paid, CancellationToken cancellationToken)
    {
        Paid.Add(paid);
        return Task.FromResult<Result<CartPaidEvent, PayCartError>>(new Result<CartPaidEvent, PayCartError>.Success(paid));
    }
}
