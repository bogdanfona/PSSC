using Laborator8_PSSC.Invoicing.Domain.Models;
using Laborator8_PSSC.Invoicing.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Laborator8_PSSC.Invoicing.EventProcessor.Data;

// Adapter: implements the invoicing port with EF Core.
public sealed class InvoicesRepository(InvoicingContext db) : IInvoicesRepository
{
    // SQL Server errors for a duplicate key in a unique index / constraint.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public async Task<InvoiceNumber?> FindByOrderAsync(int orderId, CancellationToken cancellationToken) =>
        await db.Invoices.AsNoTracking()
            .Where(i => i.OrderId == orderId)
            .Select(i => i.Number)
            .SingleOrDefaultAsync(cancellationToken) is { } number
            ? new InvoiceNumber(number)
            : null;

    public async Task<bool> SaveAsync(IssuedInvoice invoice, CancellationToken cancellationToken)
    {
        var calculated = invoice.Invoice;
        var order = calculated.Invoice;
        db.Invoices.Add(new InvoiceEntity
        {
            Number = invoice.Number.Value,
            OrderId = order.OrderId,
            CustomerCode = order.CustomerCode,
            CustomerName = order.CustomerName,
            BillingAddress = order.BillingAddress,
            IssuedAt = invoice.IssuedAt,
            NetAmount = calculated.NetAmount,
            VatAmount = calculated.VatAmount,
            Total = calculated.Total,
            Lines = [.. calculated.Lines.Select(l => new InvoiceLineEntity
            {
                ProductCode = l.Line.ProductCode,
                ProductName = l.Line.ProductName,
                Quantity = l.Line.Quantity,
                UnitPrice = l.Line.UnitPrice,
                NetAmount = l.NetAmount,
                VatAmount = l.VatAmount,
                Total = l.Total,
            })],
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        // The unique index on OrderId: another delivery of the same event saved its invoice first.
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
