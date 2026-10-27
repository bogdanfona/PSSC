using System.Globalization;
using Laborator1_PSSC.Models;

ShoppingCart? cart = null;

while (true)
{
    Console.WriteLine();
    Console.WriteLine("===== MENIU =====");
    Console.WriteLine("1. Creeaza cos gol");
    Console.WriteLine("2. Adauga produs");
    Console.WriteLine("3. Elimina produs");
    Console.WriteLine("4. Afiseaza cosul");
    Console.WriteLine("0. Iesire");
    Console.Write("Optiune: ");

    var option = Console.ReadLine()?.Trim();
    try
    {
        cart = option switch
        {
            "1" => CreateCart(),
            "2" => AddProduct(RequireCart(cart)),
            "3" => RemoveProduct(RequireCart(cart)),
            "4" => ShowCart(RequireCart(cart)),
            "0" => null,
            _ => Invalid(cart)
        };
        if (option == "0") break;
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Eroare: {ex.Message}");
    }
}

static ShoppingCart CreateCart()
{
    Console.WriteLine("S-a creat un cos de cumparaturi gol.");
    return ShoppingCart.Empty;
}

static ShoppingCart RequireCart(ShoppingCart? cart) =>
    cart ?? throw new InvalidOperationException("Nu exista un cos. Creeaza mai intai un cos (optiunea 1).");

static ShoppingCart AddProduct(ShoppingCart cart)
{
    var name = ReadNonEmpty("Nume produs: ");
    Console.Write("Tip cantitate (1 = bucati, 2 = kilograme): ");
    var type = Console.ReadLine()?.Trim();

    IQuantity quantity = type switch
    {
        "1" => new UnitQuantity(ReadInt("Numar bucati: ")),
        "2" => new KilogramQuantity(ReadDecimal("Kilograme: ")),
        _ => throw new InvalidOperationException("Tip de cantitate invalid.")
    };

    var priceLabel = quantity switch
    {
        UnitQuantity => "Pret per bucata: ",
        KilogramQuantity => "Pret per kilogram: ",
        _ => "Pret: "
    };
    var price = ReadDecimal(priceLabel);

    var updated = cart.AddItem(new CartItem(new Product(name, price), quantity));
    Console.WriteLine($"Produsul '{name}' a fost adaugat in cos.");
    return updated;
}

static ShoppingCart RemoveProduct(ShoppingCart cart)
{
    var name = ReadNonEmpty("Nume produs de eliminat: ");
    if (!cart.Contains(name))
    {
        Console.WriteLine($"Produsul '{name}' nu se afla in cos.");
        return cart;
    }
    Console.WriteLine($"Produsul '{name}' a fost eliminat.");
    return cart.RemoveItem(name);
}

static ShoppingCart ShowCart(ShoppingCart cart)
{
    if (cart.IsEmpty)
    {
        Console.WriteLine("Cosul este gol.");
        return cart;
    }

    Console.WriteLine("Continut cos:");
    foreach (var item in cart.Items)
    {
        var unit = item.Quantity switch
        {
            UnitQuantity => "buc",
            KilogramQuantity => "kg",
            _ => "?"
        };
        Console.WriteLine($" - {item.Product.Name}: {item.Quantity} x {item.Product.Price:0.00} lei/{unit} = {item.Price:0.00} lei");
    }
    Console.WriteLine($"TOTAL: {cart.TotalPrice():0.00} lei");
    return cart;
}

static ShoppingCart? Invalid(ShoppingCart? cart)
{
    Console.WriteLine("Optiune invalida.");
    return cart;
}

static string ReadNonEmpty(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        var value = Console.ReadLine()?.Trim();
        if (!string.IsNullOrEmpty(value)) return value;
        Console.WriteLine("Valoarea nu poate fi goala.");
    }
}

static int ReadInt(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        if (int.TryParse(Console.ReadLine(), out var value) && value > 0) return value;
        Console.WriteLine("Introdu un numar intreg pozitiv.");
    }
}

static decimal ReadDecimal(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        var input = Console.ReadLine()?.Trim().Replace(',', '.');
        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0)
            return value;
        Console.WriteLine("Introdu un numar pozitiv (ex: 2.5).");
    }
}
