namespace Laborator1_PSSC.Models;

public record CartItem(Product Product, IQuantity Quantity)
{
    public decimal Price => Quantity switch
    {
        UnitQuantity u => Product.Price * u.Units,
        KilogramQuantity k => Product.Price * k.Kilograms,
        _ => throw new NotSupportedException($"Cantitate necunoscuta: {Quantity.GetType().Name}")
    };
}
