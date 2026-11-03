namespace Laborator6_PSSC.Data.Queries;

// Read side, kept apart from the domain: flat records built for display, not domain types.
public sealed record ProductListItem(string Code, string Name, decimal Price, int Stock);

public sealed record CartListItem(int Id, DateTime Date, string Status, string CustomerCode, string CustomerName, int Lines, decimal Total);

public sealed record CartLineView(string ProductCode, string ProductName, decimal Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record CartView(
    int Id,
    DateTime Date,
    string Status,
    DateTime? PaidAt,
    string CustomerCode,
    string CustomerName,
    string DeliveryAddress,
    IReadOnlyList<CartLineView> Lines,
    decimal Total);

public interface IShopQueries
{
    Task<IReadOnlyList<ProductListItem>> GetProductsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CartListItem>> GetCartsAsync(CancellationToken cancellationToken);

    // Null when there is no cart with this id.
    Task<CartView?> GetCartAsync(int id, CancellationToken cancellationToken);
}
