namespace Laborator5_PSSC.ReadModels;

// Read side, kept apart from the domain: flat records built for display, not domain types.
public sealed record ProductListItem(string Code, string Name, decimal Price, int Stock);

public sealed record OrderListItem(int Id, DateTime Date, string CustomerCode, string CustomerName, string DeliveryAddress, int Lines, decimal Total);

public interface IShopQueries
{
    Task<IReadOnlyList<ProductListItem>> GetProductsAsync();
    Task<IReadOnlyList<OrderListItem>> GetOrdersAsync();
}
