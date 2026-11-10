using Laborator7_PSSC.Domain.Models;
using Laborator7_PSSC.Functional;

namespace Laborator7_PSSC.Domain.Repositories;

// Port: reads and changes a cart (a saved order). Implemented in Data/Repositories.
public interface ICartsRepository
{
    // Null when there is no order with this id.
    Task<Cart?> GetAsync(OrderId id, CancellationToken cancellationToken);

    // Takes the quantity out of stock and adds it to the cart, all or nothing. Re-checks atomically
    // what the workflow checked on loaded state: the cart is still unpaid and the stock is still there.
    Task<Result<ProductAddedToCartEvent, AddProductToCartError>> AddProductAsync(ProductAddedToCartEvent added, CancellationToken cancellationToken);

    // Fails with CartAlreadyPaid if another request paid the cart after the workflow checked it.
    Task<Result<CartPaidEvent, PayCartError>> MarkPaidAsync(CartPaidEvent paid, CancellationToken cancellationToken);
}
