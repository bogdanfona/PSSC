using Laborator3_PSSC.Domain.Models;

namespace Laborator3_PSSC.Infrastructure;

// Stand-in for a real product database and delivery service.
public static class InMemoryCatalog
{
    public sealed record ProductInfo(string Name, decimal Price, int Stock);

    public static readonly IReadOnlyDictionary<string, ProductInfo> Products = new Dictionary<string, ProductInfo>
    {
        ["MER-0001"] = new("Mere (kg)", 4.20m, 50),
        ["LAP-0042"] = new("Lapte 1L", 6.50m, 10),
        ["PAI-0007"] = new("Paine", 3.80m, 5),
        ["CAF-0100"] = new("Cafea 500g", 32.90m, 0),
    };

    public static readonly IReadOnlySet<string> DeliveryCities =
        new HashSet<string>(["Timisoara", "Arad", "Cluj-Napoca", "Bucuresti", "Iasi"], StringComparer.OrdinalIgnoreCase);

    public static bool ProductExists(ProductCode code) => Products.ContainsKey(code.Value);

    public static int GetStock(ProductCode code) => Products.TryGetValue(code.Value, out var p) ? p.Stock : 0;

    public static decimal GetPrice(ProductCode code) => Products[code.Value].Price;

    public static bool IsDeliverable(Address address) => DeliveryCities.Contains(address.City);
}
