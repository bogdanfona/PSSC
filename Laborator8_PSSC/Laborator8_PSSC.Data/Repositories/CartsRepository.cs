using Laborator8_PSSC.Data.Entities;
using Laborator8_PSSC.Domain.Models;
using Laborator8_PSSC.Domain.Repositories;
using Laborator8_PSSC.Functional;
using Microsoft.EntityFrameworkCore;

namespace Laborator8_PSSC.Data.Repositories;

// Adapter: implements the domain port with EF Core.
public sealed class CartsRepository(ShoppingCartContext db) : ICartsRepository
{
    public async Task<Cart?> GetAsync(OrderId id, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.Id == id.Value)
            .Select(o => new
            {
                o.Status,
                o.PaidAt,
                Lines = o.Items.Select(i => new CartLine(i.Product.Code, i.Quantity, i.Product.Price)).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return order switch
        {
            null => null,
            { Status: OrderStatus.Paid } => new Cart.Paid(id, order.Lines, order.PaidAt!.Value),
            _ => new Cart.Unpaid(id, order.Lines),
        };
    }

    public async Task<Result<ProductAddedToCartEvent, AddProductToCartError>> AddProductAsync(
        ProductAddedToCartEvent added, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // A no-op update that only matches an unpaid cart: it locks the row until commit,
        // so a payment arriving now waits for this product to be added first.
        var stillUnpaid = await db.Orders
            .Where(o => o.Id == added.CartId.Value && o.Status == OrderStatus.Unpaid)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Unpaid), cancellationToken);
        if (stillUnpaid == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Fail(new AddProductToCartError.CartAlreadyPaid(added.CartId));
        }

        var quantity = added.Quantity.Value;
        var product = await db.Products.Where(p => p.Code == added.ProductCode.Value).Select(p => new { p.Id }).SingleAsync(cancellationToken);

        // The workflow checked the stock on loaded state; the WHERE makes the check and the update atomic.
        var taken = await db.Products
            .Where(p => p.Id == product.Id && p.Stock >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - quantity), cancellationToken);
        if (taken == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            var available = await db.Products.Where(p => p.Id == product.Id).Select(p => p.Stock).SingleAsync(cancellationToken);
            return Fail(new AddProductToCartError.InsufficientStock(added.ProductCode, quantity, available));
        }

        // OrderItem allows a product only once per order: add to the existing line, or insert one.
        var merged = await db.OrderItems
            .Where(i => i.OrderId == added.CartId.Value && i.ProductId == product.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, i => i.Quantity + quantity), cancellationToken);
        if (merged == 0)
        {
            db.OrderItems.Add(new OrderItem { OrderId = added.CartId.Value, ProductId = product.Id, Quantity = quantity });
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return new Result<ProductAddedToCartEvent, AddProductToCartError>.Success(added);
    }

    public async Task<Result<CartPaidEvent, PayCartError>> MarkPaidAsync(CartPaidEvent paid, CancellationToken cancellationToken)
    {
        // Only an unpaid cart changes, so two payments at the same time cannot both succeed.
        var updated = await db.Orders
            .Where(o => o.Id == paid.CartId.Value && o.Status == OrderStatus.Unpaid)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, OrderStatus.Paid)
                .SetProperty(o => o.PaidAt, paid.PaidAt), cancellationToken);
        if (updated == 1)
            return new Result<CartPaidEvent, PayCartError>.Success(paid);

        var paidAt = await db.Orders.Where(o => o.Id == paid.CartId.Value).Select(o => o.PaidAt).SingleAsync(cancellationToken);
        return new Result<CartPaidEvent, PayCartError>.Failure(new PayCartError.CartAlreadyPaid(paid.CartId, paidAt!.Value));
    }

    private static Result<ProductAddedToCartEvent, AddProductToCartError> Fail(AddProductToCartError error) =>
        new Result<ProductAddedToCartEvent, AddProductToCartError>.Failure(error);
}
