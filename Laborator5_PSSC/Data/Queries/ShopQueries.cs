using Laborator5_PSSC.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace Laborator5_PSSC.Data.Queries;

// Read-only projections straight into the read models; nothing is tracked.
public sealed class ShopQueries(ShoppingCartContext db) : IShopQueries
{
    public async Task<IReadOnlyList<ProductListItem>> GetProductsAsync() =>
        await db.Products.AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new ProductListItem(p.Code, p.Name, p.Price, p.Stock))
            .ToListAsync();

    public async Task<IReadOnlyList<OrderListItem>> GetOrdersAsync() =>
        await db.Orders.AsNoTracking()
            .OrderBy(o => o.Id)
            .Select(o => new OrderListItem(
                o.Id,
                o.Date,
                o.Customer.Code,
                o.Customer.Name,
                o.DeliveryAddress,
                o.Items.Count,
                o.Items.Sum(i => i.Quantity * i.Product.Price)))
            .ToListAsync();
}
