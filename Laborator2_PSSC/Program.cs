using Laborator2_PSSC.Domain;
using Laborator2_PSSC.Domain.Entities;
using Laborator2_PSSC.Domain.States;
using Laborator2_PSSC.Domain.ValueObjects;

ICart? cart = null;

while (true)
{
    Console.WriteLine();
    Console.WriteLine("===== MENIU =====");
    Console.WriteLine("1. Creeaza cos gol");
    Console.WriteLine("2. Adauga produs");
    Console.WriteLine("3. Treci cosul in starea urmatoare");
    Console.WriteLine("4. Afiseaza cosul");
    Console.WriteLine("0. Iesire");
    Console.Write("Optiune: ");

    var option = Console.ReadLine()?.Trim();
    if (option == "0") break;

    cart = (option, cart) switch
    {
        ("1", _) => CreateCart(),
        ("2" or "3" or "4", null) => NoCart(),
        ("2", not null) => AddProduct(cart),
        ("3", not null) => Advance(cart),
        ("4", not null) => Show(cart),
        _ => Invalid(cart)
    };
}

static ICart CreateCart()
{
    Console.WriteLine("Date client:");
    var address = ReadValue(() => Address.Create(
        Prompt("  Strada: "), Prompt("  Oras: "), Prompt("  Cod postal: ")));
    var customer = ReadValue(() => Customer.Create(Prompt("  Nume: "), address));

    var empty = Cart.CreateEmpty(customer);
    Console.WriteLine($"S-a creat cosul gol {empty.Id} pentru {customer.Name}.");
    return empty;
}

static ICart AddProduct(ICart cart)
{
    if (cart is Cart.Validated or Cart.Paid)
    {
        Console.WriteLine($"Nu se pot adauga produse intr-un cos {StateName(cart)}.");
        return cart;
    }

    var code = ReadValue(() => ProductCode.Create(Prompt("Cod produs (ex: MER-0001): ")));
    var quantity = ReadValue(() => Quantity.Parse(Prompt("Cantitate: ")));
    var item = new CartItem(code, quantity);

    ICart updated = cart switch
    {
        Cart.Empty e => e.AddItem(item),
        Cart.Unvalidated u => u.AddItem(item),
        _ => cart
    };
    Console.WriteLine($"Produsul {item} a fost adaugat. Starea cosului: {StateName(updated)}.");
    return updated;
}

static ICart Advance(ICart cart)
{
    ICart next = cart switch
    {
        Cart.Empty => cart,
        Cart.Unvalidated u => u.Validate(),
        Cart.Validated v => v.Pay(DateTime.Now),
        Cart.Paid => cart,
        _ => throw new ArgumentOutOfRangeException(nameof(cart))
    };

    Console.WriteLine(ReferenceEquals(next, cart)
        ? cart switch
        {
            Cart.Empty => "Cosul este gol. Adauga produse ca sa treaca in starea nevalidat.",
            _ => "Cosul este deja platit, nu mai are o stare urmatoare."
        }
        : $"Cosul a trecut din starea {StateName(cart)} in starea {StateName(next)}.");
    return next;
}

static ICart Show(ICart cart)
{
    Console.WriteLine($"Cos {cart.Id} | Client: {cart.Customer} | Stare: {StateName(cart)}");

    var details = cart switch
    {
        Cart.Empty => "  (fara produse)",
        Cart.Unvalidated u => FormatItems(u.Items),
        Cart.Validated v => $"{FormatItems(v.Items)}\n  Livrare la: {v.ShippingAddress}",
        Cart.Paid p => $"{FormatItems(p.Items)}\n  Livrare la: {p.ShippingAddress}\n  Platit la: {p.PaidAt:dd.MM.yyyy HH:mm}",
        _ => throw new ArgumentOutOfRangeException(nameof(cart))
    };
    Console.WriteLine(details);
    return cart;
}

static string StateName(ICart cart) => cart switch
{
    Cart.Empty => "gol",
    Cart.Unvalidated => "nevalidat",
    Cart.Validated => "validat",
    Cart.Paid => "platit",
    _ => throw new ArgumentOutOfRangeException(nameof(cart))
};

static string FormatItems(IEnumerable<CartItem> items) =>
    string.Join("\n", items.Select(i => $"  - {i}"));

static ICart? NoCart()
{
    Console.WriteLine("Nu exista un cos. Creeaza mai intai un cos (optiunea 1).");
    return null;
}

static ICart? Invalid(ICart? cart)
{
    Console.WriteLine("Optiune invalida.");
    return cart;
}

static string? Prompt(string text)
{
    Console.Write(text);
    return Console.ReadLine();
}

// Repeats the input until the smart constructor returns Success.
static T ReadValue<T>(Func<Result<T>> read)
{
    while (true)
    {
        switch (read())
        {
            case Result<T>.Success s:
                return s.Value;
            case Result<T>.Failure f:
                Console.WriteLine($"Eroare: {f.Error} Incearca din nou.");
                break;
        }
    }
}
