using Laborator7_PSSC.Domain.Models;
using Laborator7_PSSC.Domain.Workflows;
using Laborator7_PSSC.Functional;

namespace Laborator7_PSSC.Domain.Tests;

public class CartWorkflowsTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 10, 0, 0);
    private static readonly OrderId CartId = new(7);

    private static ProductCode Code(string value) => ((Result<ProductCode, string>.Success)ProductCode.Create(value)).Value;

    private static readonly ProductInfo Apples = new(Code("MER-0001"), "Mere (kg)", 4.20m, 50);
    private static readonly CartLine OneMilk = new("LAP-0042", 1, 6.50m);

    private static readonly Cart.Unpaid OpenCart = new(CartId, [OneMilk]);
    private static readonly Cart.Paid PaidCart = new(CartId, [OneMilk], Now.AddDays(-1));

    [Fact]
    public async Task AddProduct_UnpaidCart_SavesTheProductWithItsPrice()
    {
        var carts = new FakeCartsRepository(OpenCart);
        var workflow = new AddProductToCartWorkflow(carts, new FakeProductsRepository(Apples));

        var result = await workflow.ExecuteAsync(CartId, new UnvalidatedCartItem("mer-0001", "3"), CancellationToken.None);

        var added = Assert.IsType<Result<ProductAddedToCartEvent, AddProductToCartError>.Success>(result).Value;
        Assert.Equal("MER-0001", added.ProductCode.Value);
        Assert.Equal(3, added.Quantity.Value);
        Assert.Equal(4.20m, added.UnitPrice);
        Assert.Single(carts.Added);
    }

    [Fact]
    public void AddProduct_InvalidCodeAndQuantity_ReportsBoth()
    {
        var result = AddProductToCartWorkflow.AddProduct(
            CartId, new UnvalidatedCartItem("mere", "0"), OpenCart, new ProductCatalog([Apples]));

        var failure = Assert.IsType<Result<ProductAddedToCartEvent, AddProductToCartError>.Failure>(result);
        var error = Assert.IsType<AddProductToCartError.InvalidInput>(failure.Error);
        Assert.Equal(2, error.Reasons.Count);
    }

    [Fact]
    public void AddProduct_PaidCart_ReturnsCartAlreadyPaid()
    {
        var result = AddProductToCartWorkflow.AddProduct(
            CartId, new UnvalidatedCartItem("MER-0001", "1"), PaidCart, new ProductCatalog([Apples]));

        var failure = Assert.IsType<Result<ProductAddedToCartEvent, AddProductToCartError>.Failure>(result);
        Assert.IsType<AddProductToCartError.CartAlreadyPaid>(failure.Error);
    }

    [Fact]
    public void AddProduct_MissingCart_ReturnsCartNotFound()
    {
        var result = AddProductToCartWorkflow.AddProduct(
            CartId, new UnvalidatedCartItem("MER-0001", "1"), null, new ProductCatalog([Apples]));

        var failure = Assert.IsType<Result<ProductAddedToCartEvent, AddProductToCartError>.Failure>(result);
        Assert.IsType<AddProductToCartError.CartNotFound>(failure.Error);
    }

    [Fact]
    public void AddProduct_MoreThanInStock_ReturnsInsufficientStock()
    {
        var result = AddProductToCartWorkflow.AddProduct(
            CartId, new UnvalidatedCartItem("MER-0001", "51"), OpenCart, new ProductCatalog([Apples]));

        var failure = Assert.IsType<Result<ProductAddedToCartEvent, AddProductToCartError>.Failure>(result);
        var error = Assert.IsType<AddProductToCartError.InsufficientStock>(failure.Error);
        Assert.Equal(50, error.Available);
    }

    [Fact]
    public async Task Pay_UnpaidCart_MarksItPaidWithTheTotalAndTime()
    {
        var carts = new FakeCartsRepository(OpenCart);
        var workflow = new PayCartWorkflow(carts, new FixedTimeProvider(new DateTimeOffset(Now, TimeSpan.Zero)));

        var result = await workflow.ExecuteAsync(CartId, CancellationToken.None);

        var paid = Assert.IsType<Result<CartPaidEvent, PayCartError>.Success>(result).Value;
        Assert.Equal(6.50m, paid.Total);
        Assert.Equal(Now, paid.PaidAt);
        Assert.Single(carts.Paid);
    }

    [Fact]
    public void Pay_PaidCart_ReturnsCartAlreadyPaid()
    {
        var result = PayCartWorkflow.Pay(CartId, PaidCart, Now);

        var failure = Assert.IsType<Result<CartPaidEvent, PayCartError>.Failure>(result);
        Assert.IsType<PayCartError.CartAlreadyPaid>(failure.Error);
    }

    [Fact]
    public void Pay_EmptyCart_ReturnsCartEmpty()
    {
        var result = PayCartWorkflow.Pay(CartId, new Cart.Unpaid(CartId, []), Now);

        var failure = Assert.IsType<Result<CartPaidEvent, PayCartError>.Failure>(result);
        Assert.IsType<PayCartError.CartEmpty>(failure.Error);
    }

    [Fact]
    public void Pay_MissingCart_ReturnsCartNotFound()
    {
        var result = PayCartWorkflow.Pay(CartId, null, Now);

        var failure = Assert.IsType<Result<CartPaidEvent, PayCartError>.Failure>(result);
        Assert.IsType<PayCartError.CartNotFound>(failure.Error);
    }
}
