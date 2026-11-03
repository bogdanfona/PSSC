using System.Globalization;
using Laborator4_PSSC.Data;
using Laborator4_PSSC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("Lipseste ConnectionStrings:DefaultConnection. Seteaz-o cu dotnet user-secrets (vezi README).");
    return;
}

// Logs only the SQL commands EF Core sends to the database.
var options = new DbContextOptionsBuilder<ShoppingCartContext>()
    .UseSqlServer(connectionString)
    .LogTo(Console.WriteLine, [DbLoggerCategory.Database.Command.Name], LogLevel.Information)
    .Options;

while (true)
{
    Console.WriteLine();
    Console.WriteLine("===== MENIU =====");
    Console.WriteLine("1. Adauga produs");
    Console.WriteLine("2. Adauga client");
    Console.WriteLine("3. Adauga comanda");
    Console.WriteLine("4. Adauga produs in comanda");
    Console.WriteLine("5. Afiseaza produse");
    Console.WriteLine("6. Afiseaza clienti");
    Console.WriteLine("7. Afiseaza comenzi");
    Console.WriteLine("8. Afiseaza produse din comenzi");
    Console.WriteLine("0. Iesire");
    Console.Write("Optiune: ");

    var option = Console.ReadLine()?.Trim();
    if (option == "0") break;

    try
    {
        using var db = new ShoppingCartContext(options);
        Action<ShoppingCartContext> action = option switch
        {
            "1" => AddProduct,
            "2" => AddCustomer,
            "3" => AddOrder,
            "4" => AddOrderItem,
            "5" => ShowProducts,
            "6" => ShowCustomers,
            "7" => ShowOrders,
            "8" => ShowOrderItems,
            _ => _ => Console.WriteLine("Optiune invalida.")
        };
        action(db);
    }
    catch (DbUpdateException ex)
    {
        Console.WriteLine($"Eroare la salvare: {ex.InnerException?.Message ?? ex.Message}");
    }
    catch (Microsoft.Data.SqlClient.SqlException ex)
    {
        Console.WriteLine($"Eroare SQL Server: {ex.Message}");
    }
}

static void AddProduct(ShoppingCartContext db)
{
    var code = ReadText("Cod produs: ").ToUpperInvariant();
    if (db.Products.Any(p => p.Code == code))
    {
        Console.WriteLine($"Exista deja un produs cu codul {code}.");
        return;
    }

    var product = new Product
    {
        Code = code,
        Name = ReadText("Nume: "),
        Price = ReadDecimal("Pret: "),
        QuantityType = ReadText("Tip cantitate (1 = bucata, 2 = kg): ") switch
        {
            "2" => "Kg",
            _ => "Unit"
        }
    };
    db.Products.Add(product);
    db.SaveChanges();
    Console.WriteLine($"Produsul {product.Code} a fost adaugat cu Id = {product.Id}.");
}

static void AddCustomer(ShoppingCartContext db)
{
    var code = ReadText("Cod client: ").ToUpperInvariant();
    if (db.Customers.Any(c => c.Code == code))
    {
        Console.WriteLine($"Exista deja un client cu codul {code}.");
        return;
    }

    var customer = new Customer { Code = code, Name = ReadText("Nume: ") };
    db.Customers.Add(customer);
    db.SaveChanges();
    Console.WriteLine($"Clientul {customer.Code} a fost adaugat cu Id = {customer.Id}.");
}

static void AddOrder(ShoppingCartContext db)
{
    var code = ReadText("Cod client: ").ToUpperInvariant();
    var customer = db.Customers.SingleOrDefault(c => c.Code == code);
    if (customer is null)
    {
        Console.WriteLine($"Nu exista clientul {code}.");
        return;
    }

    var order = new Order
    {
        Date = DateTime.Now,
        DeliveryAddress = ReadText("Adresa de livrare: "),
        CustomerId = customer.Id
    };
    db.Orders.Add(order);
    db.SaveChanges();
    Console.WriteLine($"Comanda {order.Id} a fost creata pentru {customer.Name}.");
}

// If the product is already on the order, its quantity is updated instead of adding a new row.
static void AddOrderItem(ShoppingCartContext db)
{
    var orderId = ReadInt("Id comanda: ");
    if (!db.Orders.Any(o => o.Id == orderId))
    {
        Console.WriteLine($"Nu exista comanda {orderId}.");
        return;
    }

    var code = ReadText("Cod produs: ").ToUpperInvariant();
    var product = db.Products.SingleOrDefault(p => p.Code == code);
    if (product is null)
    {
        Console.WriteLine($"Nu exista produsul {code}.");
        return;
    }

    var quantity = ReadDecimal(product.QuantityType == "Kg" ? "Cantitate (kg): " : "Cantitate (bucati): ");
    if (product.QuantityType == "Unit" && quantity != decimal.Truncate(quantity))
    {
        Console.WriteLine("Produsul se vinde la bucata, cantitatea trebuie sa fie un numar intreg.");
        return;
    }

    var item = db.OrderItems.SingleOrDefault(i => i.OrderId == orderId && i.ProductId == product.Id);
    if (item is null)
    {
        db.OrderItems.Add(new OrderItem { OrderId = orderId, ProductId = product.Id, Quantity = quantity });
        Console.WriteLine($"Produsul {code} a fost adaugat in comanda {orderId}.");
    }
    else
    {
        item.Quantity += quantity;
        Console.WriteLine($"Cantitatea pentru {code} in comanda {orderId} a fost actualizata la {item.Quantity:0.###}.");
    }
    db.SaveChanges();
}

static void ShowProducts(ShoppingCartContext db)
{
    var products = db.Products.AsNoTracking().OrderBy(p => p.Code).ToList();
    Console.WriteLine($"{"Id",-4} {"Cod",-10} {"Nume",-20} {"Pret",10} {"Tip",-5}");
    foreach (var p in products)
        Console.WriteLine($"{p.Id,-4} {p.Code,-10} {p.Name,-20} {p.Price,10:0.00} {p.QuantityType,-5}");
    Console.WriteLine($"({products.Count} inregistrari)");
}

static void ShowCustomers(ShoppingCartContext db)
{
    var customers = db.Customers.AsNoTracking().OrderBy(c => c.Code).ToList();
    Console.WriteLine($"{"Id",-4} {"Cod",-10} {"Nume",-30}");
    foreach (var c in customers)
        Console.WriteLine($"{c.Id,-4} {c.Code,-10} {c.Name,-30}");
    Console.WriteLine($"({customers.Count} inregistrari)");
}

static void ShowOrders(ShoppingCartContext db)
{
    var orders = db.Orders.AsNoTracking()
        .OrderBy(o => o.Id)
        .Select(o => new
        {
            o.Id,
            o.Date,
            o.DeliveryAddress,
            Customer = o.Customer.Name,
            Total = o.Items.Sum(i => i.Quantity * i.Product.Price)
        })
        .ToList();
    Console.WriteLine($"{"Id",-4} {"Data",-17} {"Client",-20} {"Adresa",-30} {"Total",10}");
    foreach (var o in orders)
        Console.WriteLine($"{o.Id,-4} {o.Date,-17:dd.MM.yyyy HH:mm} {o.Customer,-20} {o.DeliveryAddress,-30} {o.Total,10:0.00}");
    Console.WriteLine($"({orders.Count} inregistrari)");
}

static void ShowOrderItems(ShoppingCartContext db)
{
    var items = db.OrderItems.AsNoTracking()
        .Include(i => i.Product)
        .OrderBy(i => i.OrderId).ThenBy(i => i.Id)
        .ToList();
    Console.WriteLine($"{"Id",-4} {"Comanda",-8} {"Produs",-10} {"Cantitate",10} {"Pret",10} {"Subtotal",10}");
    foreach (var i in items)
        Console.WriteLine($"{i.Id,-4} {i.OrderId,-8} {i.Product.Code,-10} {i.Quantity,10:0.###} {i.Product.Price,10:0.00} {i.Quantity * i.Product.Price,10:0.00}");
    Console.WriteLine($"({items.Count} inregistrari)");
}

static string ReadText(string prompt)
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
