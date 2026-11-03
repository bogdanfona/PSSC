using Laborator5_PSSC.Application;
using Laborator5_PSSC.Domain;
using Laborator5_PSSC.Domain.Models;
using Laborator5_PSSC.Domain.Repositories;

namespace Laborator5_PSSC.Tests;

public class PlaceOrderServiceTests
{
    // In-memory fakes for the ports, so the service is tested without a database.
    private sealed class FakeProductsRepository(params ProductInfo[] products) : IProductsRepository
    {
        public List<string> RequestedCodes { get; } = [];

        public Task<ProductCatalog> GetByCodesAsync(IEnumerable<ProductCode> codes)
        {
            var requested = codes.ToList();
            RequestedCodes.AddRange(requested.Select(c => c.Value));
            return Task.FromResult(new ProductCatalog(products.Where(p => requested.Contains(p.Code))));
        }
    }

    private sealed class FakeOrdersRepository : IOrdersRepository
    {
        public List<OrderPlacedEvent> Saved { get; } = [];

        public Task<Result<OrderId, PlaceOrderError>> SaveAsync(OrderPlacedEvent order)
        {
            Saved.Add(order);
            return Task.FromResult<Result<OrderId, PlaceOrderError>>(
                new Result<OrderId, PlaceOrderError>.Success(new OrderId(Saved.Count)));
        }
    }

    private static ProductCode Code(string value) => ((Result<ProductCode, string>.Success)ProductCode.Create(value)).Value;

    private static UnvalidatedOrder Order(params UnvalidatedOrderLine[] lines) =>
        new("C-100", "Ana Pop", "Str. Lalelelor 5", "Timisoara", "300001", lines);

    private static readonly ProductInfo Apples = new(Code("MER-0001"), "Mere (kg)", 4.20m, 50);
    private static readonly ProductInfo Coffee = new(Code("CAF-0100"), "Cafea 500g", 32.90m, 0);

    [Fact]
    public async Task ExecuteAsync_ValidOrder_LoadsProductsAndSavesTheEvent()
    {
        var products = new FakeProductsRepository(Apples, Coffee);
        var orders = new FakeOrdersRepository();
        var service = new PlaceOrderService(products, orders, _ => true);

        var result = await service.ExecuteAsync(Order(new UnvalidatedOrderLine("mer-0001", "3"), new UnvalidatedOrderLine("MER-0001", "2")));

        var success = Assert.IsType<Result<SavedOrder, PlaceOrderError>.Success>(result);
        Assert.Equal(new OrderId(1), success.Value.OrderId);
        Assert.Equal(21.00m, success.Value.Order.Total);
        Assert.Equal("C-100", Assert.Single(orders.Saved).CustomerCode.Value);
        Assert.Equal(["MER-0001"], products.RequestedCodes);
    }

    [Fact]
    public async Task ExecuteAsync_ProductOutOfStock_ReturnsErrorAndSavesNothing()
    {
        var orders = new FakeOrdersRepository();
        var service = new PlaceOrderService(new FakeProductsRepository(Apples, Coffee), orders, _ => true);

        var result = await service.ExecuteAsync(Order(new UnvalidatedOrderLine("CAF-0100", "1")));

        var failure = Assert.IsType<Result<SavedOrder, PlaceOrderError>.Failure>(result);
        var error = Assert.IsType<PlaceOrderError.InsufficientStock>(failure.Error);
        Assert.Equal(0, error.Available);
        Assert.Empty(orders.Saved);
    }

    [Fact]
    public async Task ExecuteAsync_ProductNotInDatabase_ReturnsProductsNotFound()
    {
        var orders = new FakeOrdersRepository();
        var service = new PlaceOrderService(new FakeProductsRepository(Apples), orders, _ => true);

        var result = await service.ExecuteAsync(Order(new UnvalidatedOrderLine("XYZ-9999", "1")));

        var failure = Assert.IsType<Result<SavedOrder, PlaceOrderError>.Failure>(result);
        var error = Assert.IsType<PlaceOrderError.ProductsNotFound>(failure.Error);
        Assert.Equal("XYZ-9999", Assert.Single(error.Codes).Value);
        Assert.Empty(orders.Saved);
    }
}
