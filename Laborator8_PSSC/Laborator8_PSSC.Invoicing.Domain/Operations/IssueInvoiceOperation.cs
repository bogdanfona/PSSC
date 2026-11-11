using Laborator8_PSSC.Invoicing.Domain.Models;

namespace Laborator8_PSSC.Invoicing.Domain.Operations;

// Gives the calculated invoice its number and issue date.
public static class IssueInvoiceOperation
{
    public static IssuedInvoice Execute(CalculatedInvoice invoice, DateTime now) =>
        new(InvoiceNumber.For(invoice.Invoice.OrderId, invoice.Invoice.PlacedAt), now, invoice);
}
