using System.Collections.Immutable;

namespace Laborator1_PSSC.Models;

public record ShoppingCart(ImmutableList<CartItem> Items)
{
    public static ShoppingCart Empty => new(ImmutableList<CartItem>.Empty);

    public bool IsEmpty => Items.IsEmpty;

    public ShoppingCart AddItem(CartItem item)
    {
        var existing = Items.FirstOrDefault(i => SameName(i.Product, item.Product));
        if (existing is null)
            return this with { Items = Items.Add(item) };

        // Same product already in cart: combine quantities if they are of the same kind.
        var combined = (existing.Quantity, item.Quantity) switch
        {
            (UnitQuantity a, UnitQuantity b) => existing with { Quantity = a with { Units = a.Units + b.Units } },
            (KilogramQuantity a, KilogramQuantity b) => existing with { Quantity = a with { Kilograms = a.Kilograms + b.Kilograms } },
            _ => throw new InvalidOperationException(
                $"Produsul '{existing.Product.Name}' este deja in cos cu alt tip de cantitate.")
        };
        return this with { Items = Items.Replace(existing, combined) };
    }

    public ShoppingCart RemoveItem(string productName)
    {
        var existing = Items.FirstOrDefault(i => SameName(i.Product, new Product(productName, 0)));
        return existing is null ? this : this with { Items = Items.Remove(existing) };
    }

    public bool Contains(string productName) =>
        Items.Any(i => string.Equals(i.Product.Name, productName, StringComparison.OrdinalIgnoreCase));

    public decimal TotalPrice() => Items.Sum(i => i.Price);

    private static bool SameName(Product a, Product b) =>
        string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
}
