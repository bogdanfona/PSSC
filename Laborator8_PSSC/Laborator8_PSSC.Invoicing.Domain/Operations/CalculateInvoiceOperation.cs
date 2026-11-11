using Laborator8_PSSC.Functional;
using Laborator8_PSSC.Invoicing.Domain.Models;

namespace Laborator8_PSSC.Invoicing.Domain.Operations;

// Splits every line into net amount and VAT, and checks that the invoice adds up to what the customer paid.
public static class CalculateInvoiceOperation
{
    // Standard VAT rate in Romania (since August 2025). Shop prices already include it.
    public const decimal VatRate = 0.21m;

    public static Result<CalculatedInvoice, GenerateInvoiceError> Execute(ValidatedInvoice invoice)
    {
        var lines = invoice.Lines.Select(CalculateLine).ToList();
        var total = lines.Sum(l => l.Total);

        // The invoice is built from the same lines the customer paid for; a different total means corrupt data.
        if (total != decimal.Round(invoice.OrderTotal, 2, MidpointRounding.AwayFromZero))
            return new Result<CalculatedInvoice, GenerateInvoiceError>.Failure(new GenerateInvoiceError.TotalMismatch(invoice.OrderTotal, total));

        return new Result<CalculatedInvoice, GenerateInvoiceError>.Success(
            new CalculatedInvoice(invoice, lines, lines.Sum(l => l.NetAmount), lines.Sum(l => l.VatAmount), total));
    }

    public static CalculatedInvoiceLine CalculateLine(InvoiceLine line)
    {
        var total = Round(line.Quantity * line.UnitPrice);
        var net = Round(total / (1 + VatRate));
        // VAT is the difference, so net + VAT is exactly the line total, with no rounding cent lost.
        return new CalculatedInvoiceLine(line, net, total - net, total);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
