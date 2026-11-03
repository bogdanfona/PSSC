using Laborator5_PSSC.ConsoleApp;
using Laborator5_PSSC.Data;
using Laborator5_PSSC.Data.Queries;
using Laborator5_PSSC.Domain.Models;
using Laborator5_PSSC.Domain.Workflows;
using Laborator5_PSSC.Functional;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// appsettings.json is next to the executable; dotnet run keeps the caller's folder as current directory.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// The host loads user secrets only in Development; this app always reads them, like Lab 4.
builder.Configuration.AddUserSecrets<Program>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("Lipseste ConnectionStrings:DefaultConnection. Seteaz-o cu dotnet user-secrets (vezi README).");
    return;
}

builder.Services.AddShoppingCartData(connectionString);
builder.Services.AddScoped<PlaceOrderWorkflow>();
builder.Services.AddSingleton<CheckAddressDeliverable>(DeliveryZones.IsDeliverable);
builder.Services.AddSingleton(TimeProvider.System);

using var host = builder.Build();

// Ctrl+C cancels the database call in progress instead of killing the app.
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
};

while (true)
{
    Console.WriteLine();
    Console.WriteLine("===== MENIU =====");
    Console.WriteLine("1. Afiseaza catalogul");
    Console.WriteLine("2. Plaseaza o comanda");
    Console.WriteLine("3. Afiseaza comenzile");
    Console.WriteLine("0. Iesire");
    var option = Prompt("Optiune: ").Trim();
    if (option == "0" || cancellation.IsCancellationRequested) break;

    try
    {
        // One scope per action: a fresh DbContext, so a failed save leaves nothing tracked behind.
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var ct = cancellation.Token;
        switch (option)
        {
            case "1": await ShowProducts(services.GetRequiredService<IShopQueries>(), ct); break;
            case "2": await PlaceOrder(services.GetRequiredService<PlaceOrderWorkflow>(), ct); break;
            case "3": await ShowOrders(services.GetRequiredService<IShopQueries>(), ct); break;
            default: Console.WriteLine("Optiune invalida."); break;
        }
    }
    catch (Microsoft.Data.SqlClient.SqlException ex)
    {
        Console.WriteLine($"Eroare SQL Server: {ex.Message}");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Operatie anulata.");
        break;
    }
}

static async Task ShowProducts(IShopQueries queries, CancellationToken ct)
{
    var products = await queries.GetProductsAsync(ct);
    Console.WriteLine($"{"Cod",-10} {"Nume",-20} {"Pret",10} {"Stoc",6}");
    foreach (var p in products)
        Console.WriteLine($"{p.Code,-10} {p.Name,-20} {p.Price,10:0.00} {p.Stock,6}");
    Console.WriteLine($"Livram in: {string.Join(", ", DeliveryZones.Cities)}");
}

static async Task ShowOrders(IShopQueries queries, CancellationToken ct)
{
    var orders = await queries.GetOrdersAsync(ct);
    Console.WriteLine($"{"Id",-4} {"Data",-17} {"Client",-24} {"Adresa",-35} {"Linii",5} {"Total",10}");
    foreach (var o in orders)
        Console.WriteLine($"{o.Id,-4} {o.Date,-17:dd.MM.yyyy HH:mm} {$"{o.CustomerCode} {o.CustomerName}",-24} {o.DeliveryAddress,-35} {o.Lines,5} {o.Total,10:0.00}");
    Console.WriteLine($"({orders.Count} comenzi)");
}

static async Task PlaceOrder(PlaceOrderWorkflow workflow, CancellationToken ct)
{
    var result = await workflow.ExecuteAsync(ReadOrder(), ct);
    Console.WriteLine();
    Console.WriteLine(result switch
    {
        Result<SavedOrder, PlaceOrderError>.Success s => DescribeOrder(s.Value),
        Result<SavedOrder, PlaceOrderError>.Failure f => DescribeError(f.Error),
        _ => throw new InvalidOperationException()
    });
}

static UnvalidatedOrder ReadOrder()
{
    Console.WriteLine();
    Console.WriteLine("===== COMANDA NOUA =====");
    var customerCode = Prompt("Cod client: ");
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

    return new UnvalidatedOrder(customerCode, name, street, city, postalCode, lines);
}

static string DescribeOrder(SavedOrder saved)
{
    var e = saved.Order;
    var lines = string.Join("\n", e.Lines.Select(l =>
        $"  - {l.ProductCode} x {l.Quantity} @ {l.UnitPrice:0.00} = {l.LineTotal:0.00} lei"));
    return $"""
        [OrderPlacedEvent] Comanda {saved.OrderId} a fost salvata in baza de date la {e.PlacedAt:dd.MM.yyyy HH:mm}.
          Client: {e.CustomerCode} {e.CustomerName}
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
