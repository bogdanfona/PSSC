namespace Laborator2_PSSC.Domain.ValueObjects;

// Built only from already validated value objects, so it needs no smart constructor.
public sealed record CartItem(ProductCode Code, Quantity Quantity)
{
    public override string ToString() => $"{Code} x {Quantity}";
}
