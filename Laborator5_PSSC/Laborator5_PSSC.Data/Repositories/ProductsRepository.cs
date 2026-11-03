using Laborator5_PSSC.Domain.Models;
using Laborator5_PSSC.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Laborator5_PSSC.Data.Repositories;

// Adapter: implements the domain port with EF Core.
public sealed class ProductsRepository(ShoppingCartContext db) : IProductsRepository
{
    public async Task<ProductCatalog> GetByCodesAsync(IReadOnlyCollection<ProductCode> codes, CancellationToken cancellationToken)
    {
        var requested = codes.Distinct().ToDictionary(c => c.Value);
        var values = requested.Keys.ToList();
        var rows = await db.Products.AsNoTracking()
            .Where(p => values.Contains(p.Code))
            .Select(p => new { p.Code, p.Name, p.Price, p.Stock })
            .ToListAsync(cancellationToken);

        return new ProductCatalog(rows.Select(r => new ProductInfo(requested[r.Code], r.Name, r.Price, r.Stock)));
    }
}
