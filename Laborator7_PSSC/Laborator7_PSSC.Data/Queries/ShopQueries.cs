using Microsoft.EntityFrameworkCore;

namespace Laborator7_PSSC.Data.Queries;

// Read-only projections straight into the read models; nothing is tracked.
internal sealed class ShopQueries(ShoppingCartContext db) : IShopQueries
{
    public async Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new ProductListItem(p.Code, p.Name, p.Price, p.Stock))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CartListItem>> GetCartsAsync(CancellationToken cancellationToken) =>
        await db.Orders.AsNoTracking()
            .OrderBy(o => o.Id)
            .Select(o => new CartListItem(
                o.Id,
                o.Date,
                o.Status,
                o.Customer.Code,
                o.Customer.Name,
                o.Items.Count,
                o.Items.Sum(i => i.Quantity * i.Product.Price)))
            .ToListAsync(cancellationToken);

    public async Task<CartView?> GetCartAsync(int id, CancellationToken cancellationToken)
    {
        var cart = await db.Orders.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new
            {
                o.Id,
                o.Date,
                o.Status,
                o.PaidAt,
                CustomerCode = o.Customer.Code,
                CustomerName = o.Customer.Name,
                o.DeliveryAddress,
                Lines = o.Items
                    .OrderBy(i => i.Product.Code)
                    .Select(i => new CartLineView(i.Product.Code, i.Product.Name, i.Quantity, i.Product.Price, i.Quantity * i.Product.Price))
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        return cart is null
            ? null
            : new CartView(cart.Id, cart.Date, cart.Status, cart.PaidAt, cart.CustomerCode, cart.CustomerName,
                cart.DeliveryAddress, cart.Lines, cart.Lines.Sum(l => l.LineTotal));
    }
}
