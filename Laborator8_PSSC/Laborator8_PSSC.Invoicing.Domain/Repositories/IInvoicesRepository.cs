using Laborator8_PSSC.Invoicing.Domain.Models;

namespace Laborator8_PSSC.Invoicing.Domain.Repositories;

// Port of the invoicing context; implemented with EF Core in the event processor.
public interface IInvoicesRepository
{
    // The invoice already issued for this order, or null.
    Task<InvoiceNumber?> FindByOrderAsync(int orderId, CancellationToken cancellationToken);

    // False when another delivery of the same event saved an invoice for this order first.
    Task<bool> SaveAsync(IssuedInvoice invoice, CancellationToken cancellationToken);
}
