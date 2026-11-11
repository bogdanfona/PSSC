using Laborator8_PSSC.Domain.Models;
using Laborator8_PSSC.Domain.Repositories;
using Laborator8_PSSC.Functional;

namespace Laborator8_PSSC.Domain.Workflows;

// Impure -> pure -> impure, like PlaceOrderWorkflow.
public sealed class AddProductToCartWorkflow(ICartsRepository cartsRepository, IProductsRepository productsRepository)
{
    public async Task<Result<ProductAddedToCartEvent, AddProductToCartError>> ExecuteAsync(
        OrderId cartId, UnvalidatedCartItem item, CancellationToken cancellationToken)
    {
        var cart = await cartsRepository.GetAsync(cartId, cancellationToken);
        var catalog = await productsRepository.GetByCodesAsync(
            ProductCode.Create(item.ProductCode) is Result<ProductCode, string>.Success code ? [code.Value] : [],
            cancellationToken);

        return AddProduct(cartId, item, cart, catalog) switch
        {
            Result<ProductAddedToCartEvent, AddProductToCartError>.Success s =>
                await cartsRepository.AddProductAsync(s.Value, cancellationToken),
            var failure => failure
        };
    }

    // The pure core: decides on the cart and product loaded before it runs.
    public static Result<ProductAddedToCartEvent, AddProductToCartError> AddProduct(
        OrderId cartId, UnvalidatedCartItem item, Cart? cart, ProductCatalog catalog)
    {
        var code = ProductCode.Create(item.ProductCode);
        var quantity = Quantity.Parse(item.Quantity);
        if ((code, quantity) is not (Result<ProductCode, string>.Success(var product), Result<Quantity, string>.Success(var requested)))
            return Fail(new AddProductToCartError.InvalidInput([.. Errors(code), .. Errors(quantity)]));

        return cart switch
        {
            null => Fail(new AddProductToCartError.CartNotFound(cartId)),
            Cart.Paid => Fail(new AddProductToCartError.CartAlreadyPaid(cartId)),
            _ when !catalog.Exists(product) => Fail(new AddProductToCartError.ProductNotFound(product)),
            _ when catalog.GetStock(product) < requested.Value =>
                Fail(new AddProductToCartError.InsufficientStock(product, requested.Value, catalog.GetStock(product))),
            _ => new Result<ProductAddedToCartEvent, AddProductToCartError>.Success(
                new ProductAddedToCartEvent(cartId, product, requested, catalog.GetPrice(product))),
        };
    }

    private static IEnumerable<string> Errors<T>(Result<T, string> result) =>
        result is Result<T, string>.Failure f ? [f.Error] : [];

    private static Result<ProductAddedToCartEvent, AddProductToCartError> Fail(AddProductToCartError error) =>
        new Result<ProductAddedToCartEvent, AddProductToCartError>.Failure(error);
}
