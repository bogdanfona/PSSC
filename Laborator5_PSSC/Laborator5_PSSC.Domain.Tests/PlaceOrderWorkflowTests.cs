using Laborator5_PSSC.Domain.Models;
using Laborator5_PSSC.Domain.Workflows;
using Laborator5_PSSC.Functional;

namespace Laborator5_PSSC.Domain.Tests;

public class PlaceOrderWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private static ProductCode Code(string value) => ((Result<ProductCode, string>.Success)ProductCode.Create(value)).Value;

    private static UnvalidatedOrder Order(params UnvalidatedOrderLine[] lines) =>
        new("C-100", "Ana Pop", "Str. Lalelelor 5", "Timisoara", "300001", lines);

    private static readonly ProductInfo Apples = new(Code("MER-0001"), "Mere (kg)", 4.20m, 50);
    private static readonly ProductInfo Coffee = new(Code("CAF-0100"), "Cafea 500g", 32.90m, 0);

    private static PlaceOrderWorkflow Workflow(FakeProductsRepository products, RecordingOrdersRepository orders) =>
        new(products, orders, _ => true, new FixedTimeProvider(Now));

    [Fact]
    public async Task ExecuteAsync_ValidOrder_LoadsProductsAndSavesTheEvent()
    {
        var products = new FakeProductsRepository(Apples, Coffee);
        var orders = new RecordingOrdersRepository();

        var result = await Workflow(products, orders).ExecuteAsync(
            Order(new UnvalidatedOrderLine("mer-0001", "3"), new UnvalidatedOrderLine("MER-0001", "2")),
            CancellationToken.None);

        var success = Assert.IsType<Result<SavedOrder, PlaceOrderError>.Success>(result);
        Assert.Equal(new OrderId(1), success.Value.OrderId);
        Assert.Equal(21.00m, success.Value.Order.Total);
        Assert.Equal(Now.DateTime, success.Value.Order.PlacedAt);
        Assert.Equal("C-100", Assert.Single(orders.Saved).CustomerCode.Value);
        Assert.Equal(["MER-0001"], products.RequestedCodes);
    }

    [Fact]
    public async Task ExecuteAsync_ProductOutOfStock_ReturnsErrorAndSavesNothing()
    {
        var orders = new RecordingOrdersRepository();

        var result = await Workflow(new FakeProductsRepository(Apples, Coffee), orders).ExecuteAsync(
            Order(new UnvalidatedOrderLine("CAF-0100", "1")), CancellationToken.None);

        var failure = Assert.IsType<Result<SavedOrder, PlaceOrderError>.Failure>(result);
        var error = Assert.IsType<PlaceOrderError.InsufficientStock>(failure.Error);
        Assert.Equal(0, error.Available);
        Assert.Empty(orders.Saved);
    }

    [Fact]
    public async Task ExecuteAsync_ProductNotInDatabase_ReturnsProductsNotFound()
    {
        var orders = new RecordingOrdersRepository();

        var result = await Workflow(new FakeProductsRepository(Apples), orders).ExecuteAsync(
            Order(new UnvalidatedOrderLine("XYZ-9999", "1")), CancellationToken.None);

        var failure = Assert.IsType<Result<SavedOrder, PlaceOrderError>.Failure>(result);
        var error = Assert.IsType<PlaceOrderError.ProductsNotFound>(failure.Error);
        Assert.Equal("XYZ-9999", Assert.Single(error.Codes).Value);
        Assert.Empty(orders.Saved);
    }

    [Fact]
    public void Place_UndeliverableCity_ReturnsUndeliverableAddress()
    {
        // The pure core needs no repositories: the catalog and the clock are plain arguments.
        var catalog = new ProductCatalog([Apples]);

        var result = PlaceOrderWorkflow.Place(
            Order(new UnvalidatedOrderLine("MER-0001", "1")), catalog, _ => false, Now.DateTime);

        var failure = Assert.IsType<Result<OrderPlacedEvent, PlaceOrderError>.Failure>(result);
        Assert.IsType<PlaceOrderError.UndeliverableAddress>(failure.Error);
    }
}
