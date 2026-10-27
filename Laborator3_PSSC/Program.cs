using Laborator3_PSSC.Domain;
using Laborator3_PSSC.Domain.Models;
using Laborator3_PSSC.Domain.Workflows;
using Laborator3_PSSC.Infrastructure;

var workflow = new PlaceOrderWorkflow(
    InMemoryCatalog.ProductExists,
    InMemoryCatalog.GetStock,
    InMemoryCatalog.IsDeliverable,
    InMemoryCatalog.GetPrice);

PrintCatalog();

do
{
    var order = ReadOrder();
    var result = workflow.Execute(order);

    Console.WriteLine();
    Console.WriteLine(result switch
    {
        Result<OrderPlacedEvent, PlaceOrderError>.Success s => DescribeEvent(s.Value),
        Result<OrderPlacedEvent, PlaceOrderError>.Failure f => DescribeError(f.Error),
        _ => throw new InvalidOperationException()
    });
} while (Prompt("\nPlasezi alta comanda? (d/n): ").Trim().Equals("d", StringComparison.OrdinalIgnoreCase));

static void PrintCatalog()
{
    Console.WriteLine("===== CATALOG =====");
    foreach (var (code, p) in InMemoryCatalog.Products)
        Console.WriteLine($"  {code}  {p.Name,-12} {p.Price,7:0.00} lei   stoc: {p.Stock}");
    Console.WriteLine($"Livram in: {string.Join(", ", InMemoryCatalog.DeliveryCities)}");
}

static UnvalidatedOrder ReadOrder()
{
    Console.WriteLine();
    Console.WriteLine("===== COMANDA NOUA =====");
    var name = Prompt("Nume client: ");
    var street = Prompt("Strada: ");
    var city = Prompt("Oras: ");
    var postalCode = Prompt("Cod postal: ");

    Console.WriteLine("Produse (lasa codul gol ca sa termini):");
    var lines = new List<UnvalidatedOrderLine>();
    while (true)
    {
        var code = Prompt($"  Cod produs #{lines.Count + 1}: ");
        if (string.IsNullOrWhiteSpace(code)) break;
        lines.Add(new UnvalidatedOrderLine(code, Prompt("  Cantitate: ")));
    }

    return new UnvalidatedOrder(name, street, city, postalCode, lines);
}

static string DescribeEvent(OrderPlacedEvent e)
{
    var lines = string.Join("\n", e.Lines.Select(l =>
        $"  - {l.ProductCode} x {l.Quantity} @ {l.UnitPrice:0.00} = {l.LineTotal:0.00} lei"));
    return $"""
        [OrderPlacedEvent] Comanda {e.OrderId} a fost plasata la {e.PlacedAt:dd.MM.yyyy HH:mm}.
          Client: {e.CustomerName}
          Livrare: {e.ShippingAddress}
        {lines}
          TOTAL: {e.Total:0.00} lei
        """;
}

static string DescribeError(PlaceOrderError error) => "[PlaceOrderError] " + error switch
{
    PlaceOrderError.InvalidInput e => "Date invalide:\n" + string.Join("\n", e.Reasons.Select(r => $"  - {r}")),
    PlaceOrderError.ProductsNotFound e => $"Produse inexistente: {string.Join(", ", e.Codes)}",
    PlaceOrderError.InsufficientStock e => $"Stoc insuficient pentru {e.Code}: cerut {e.Requested}, disponibil {e.Available}.",
    PlaceOrderError.UndeliverableAddress e => $"Nu livram la adresa {e.Address}.",
    _ => throw new InvalidOperationException()
};

static string Prompt(string text)
{
    Console.Write(text);
    return Console.ReadLine() ?? "";
}
