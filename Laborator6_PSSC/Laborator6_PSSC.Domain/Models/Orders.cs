using Laborator6_PSSC.Functional;

namespace Laborator6_PSSC.Domain.Models;

// Raw input, exactly as it comes from outside the domain.
public sealed record UnvalidatedOrderLine(string ProductCode, string Quantity);

public sealed record UnvalidatedOrder(
    string CustomerCode,
    string CustomerName,
    string Street,
    string City,
    string PostalCode,
    IReadOnlyList<UnvalidatedOrderLine> Lines)
{
    // The products to load from the database before the workflow runs. Invalid codes are skipped here;
    // the validation step reports them.
    public IReadOnlyCollection<ProductCode> ProductCodes =>
    [
        .. Lines
            .Select(l => ProductCode.Create(l.ProductCode))
            .OfType<Result<ProductCode, string>.Success>()
            .Select(s => s.Value)
            .Distinct(),
    ];
}

// Input converted to domain types.
public sealed record ValidatedOrderLine(ProductCode ProductCode, Quantity Quantity);

public sealed record ValidatedOrder(
    CustomerCode CustomerCode,
    CustomerName CustomerName,
    Address ShippingAddress,
    IReadOnlyList<ValidatedOrderLine> Lines);

// Order with prices attached, ready to be placed.
public sealed record PricedOrderLine(ProductCode ProductCode, Quantity Quantity, decimal UnitPrice)
{
    public decimal LineTotal => UnitPrice * Quantity.Value;
}

public sealed record PricedOrder(
    CustomerCode CustomerCode,
    CustomerName CustomerName,
    Address ShippingAddress,
    IReadOnlyList<PricedOrderLine> Lines)
{
    public decimal Total => Lines.Sum(l => l.LineTotal);
}
