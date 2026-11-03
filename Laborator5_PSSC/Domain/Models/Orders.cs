namespace Laborator5_PSSC.Domain.Models;

// Raw input, exactly as it comes from outside the domain.
public sealed record UnvalidatedOrderLine(string ProductCode, string Quantity);

public sealed record UnvalidatedOrder(
    string CustomerCode,
    string CustomerName,
    string Street,
    string City,
    string PostalCode,
    IReadOnlyList<UnvalidatedOrderLine> Lines);

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
