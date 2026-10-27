namespace Laborator3_PSSC.Domain.Models;

// Why the workflow could not produce an OrderPlacedEvent. Closed hierarchy.
public abstract record PlaceOrderError
{
    private PlaceOrderError() { }

    public sealed record InvalidInput(IReadOnlyList<string> Reasons) : PlaceOrderError;
    public sealed record ProductsNotFound(IReadOnlyList<ProductCode> Codes) : PlaceOrderError;
    public sealed record InsufficientStock(ProductCode Code, int Requested, int Available) : PlaceOrderError;
    public sealed record UndeliverableAddress(Address Address) : PlaceOrderError;
}
