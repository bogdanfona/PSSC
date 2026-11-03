using Laborator5_PSSC.Data.Entities;
using Laborator5_PSSC.Domain;
using Laborator5_PSSC.Domain.Models;
using Laborator5_PSSC.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Laborator5_PSSC.Data.Repositories;

// Adapter: implements the domain port with EF Core.
public sealed class OrdersRepository(ShoppingCartContext db) : IOrdersRepository
{
    public async Task<Result<OrderId, PlaceOrderError>> SaveAsync(OrderPlacedEvent placed)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Upsert by code: an existing customer keeps its Id and gets the name from this order.
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Code == placed.CustomerCode.Value);
        if (customer is null)
        {
            customer = new Customer { Code = placed.CustomerCode.Value };
            db.Customers.Add(customer);
        }
        customer.Name = placed.CustomerName.Value;

        // OrderItem allows a product only once per order, so lines with the same product are merged.
        var quantities = placed.Lines
            .GroupBy(l => l.ProductCode.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity.Value));
        var codes = quantities.Keys.ToList();
        var products = await db.Products
            .Where(p => codes.Contains(p.Code))
            .ToDictionaryAsync(p => p.Code);

        // The workflow checked the stock on a snapshot; the WHERE makes the check and the update atomic,
        // so a concurrent order cannot take the stock below zero.
        foreach (var (code, quantity) in quantities)
        {
            var product = products[code];
            var updated = await db.Products
                .Where(p => p.Id == product.Id && p.Stock >= quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - quantity));
            if (updated == 0)
            {
                await transaction.RollbackAsync();
                var available = await db.Products.Where(p => p.Id == product.Id).Select(p => p.Stock).SingleAsync();
                var productCode = placed.Lines.First(l => l.ProductCode.Value == code).ProductCode;
                return new Result<OrderId, PlaceOrderError>.Failure(
                    new PlaceOrderError.InsufficientStock(productCode, quantity, available));
            }
        }

        var order = new Order
        {
            Date = placed.PlacedAt,
            DeliveryAddress = placed.ShippingAddress.ToString(),
            Customer = customer,
            Items = quantities
                .Select(q => new OrderItem { ProductId = products[q.Key].Id, Quantity = q.Value })
                .ToList()
        };
        db.Orders.Add(order);

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new Result<OrderId, PlaceOrderError>.Success(new OrderId(order.Id));
    }
}
