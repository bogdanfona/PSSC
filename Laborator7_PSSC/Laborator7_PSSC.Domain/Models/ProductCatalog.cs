namespace Laborator7_PSSC.Domain.Models;

public sealed record ProductInfo(ProductCode Code, string Name, decimal Price, int Stock);

// Product state loaded from the database before the workflow runs.
// Its methods match the workflow's dependencies, so the workflow itself does no I/O.
public sealed class ProductCatalog(IEnumerable<ProductInfo> products)
{
    private readonly Dictionary<ProductCode, ProductInfo> _products = products.ToDictionary(p => p.Code);

    public bool Exists(ProductCode code) => _products.ContainsKey(code);

    public int GetStock(ProductCode code) => _products.TryGetValue(code, out var p) ? p.Stock : 0;

    public decimal GetPrice(ProductCode code) => _products[code].Price;
}
