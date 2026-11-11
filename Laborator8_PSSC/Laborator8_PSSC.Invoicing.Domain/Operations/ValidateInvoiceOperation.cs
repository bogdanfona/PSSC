using Laborator8_PSSC.Functional;
using Laborator8_PSSC.Invoicing.Domain.Models;

namespace Laborator8_PSSC.Invoicing.Domain.Operations;

// Checks the data received from the order and reports every problem at once.
public static class ValidateInvoiceOperation
{
    public static Result<ValidatedInvoice, GenerateInvoiceError> Execute(UnvalidatedInvoice invoice)
    {
        var reasons = new List<string>();

        if (invoice.OrderId <= 0)
            reasons.Add("Id-ul comenzii trebuie sa fie pozitiv.");
        var customerCode = Required(invoice.CustomerCode, "Codul clientului lipseste.", reasons);
        var customerName = Required(invoice.CustomerName, "Numele clientului lipseste.", reasons);
        var address = Required(invoice.BillingAddress, "Adresa de facturare lipseste.", reasons);

        if (invoice.Lines.Count == 0)
            reasons.Add("Comanda nu are produse.");

        var lines = new List<InvoiceLine>();
        foreach (var (line, index) in invoice.Lines.Select((l, i) => (l, i + 1)))
        {
            var code = Required(line.ProductCode, $"Linia {index}: codul produsului lipseste.", reasons);
            if (line.Quantity <= 0)
                reasons.Add($"Linia {index}: cantitatea trebuie sa fie pozitiva.");
            if (line.UnitPrice < 0)
                reasons.Add($"Linia {index}: pretul nu poate fi negativ.");
            lines.Add(new InvoiceLine(code, string.IsNullOrWhiteSpace(line.ProductName) ? code : line.ProductName.Trim(), line.Quantity, line.UnitPrice));
        }

        return reasons.Count > 0
            ? new Result<ValidatedInvoice, GenerateInvoiceError>.Failure(new GenerateInvoiceError.InvalidOrder(reasons))
            : new Result<ValidatedInvoice, GenerateInvoiceError>.Success(
                new ValidatedInvoice(invoice.OrderId, customerCode, customerName, address, lines, invoice.OrderTotal, invoice.PlacedAt));
    }

    private static string Required(string? value, string reason, List<string> reasons)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            reasons.Add(reason);
            return "";
        }
        return value.Trim();
    }
}
