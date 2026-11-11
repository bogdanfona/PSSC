namespace Laborator8_PSSC.Invoicing.Domain.Models;

// The invoice moves through states; each state is its own type, so an operation can only take the state it expects:
// Unvalidated (raw data from the order) -> Validated -> Calculated (VAT split) -> Issued (has a number and a date).

public sealed record UnvalidatedInvoiceLine(string? ProductCode, string? ProductName, decimal Quantity, decimal UnitPrice);

// The command: the order as received from the event, nothing checked yet.
public sealed record UnvalidatedInvoice(
    int OrderId,
    string? CustomerCode,
    string? CustomerName,
    string? BillingAddress,
    IReadOnlyList<UnvalidatedInvoiceLine> Lines,
    decimal OrderTotal,
    DateTime PlacedAt);

public sealed record InvoiceLine(string ProductCode, string ProductName, decimal Quantity, decimal UnitPrice);

public sealed record ValidatedInvoice(
    int OrderId,
    string CustomerCode,
    string CustomerName,
    string BillingAddress,
    IReadOnlyList<InvoiceLine> Lines,
    decimal OrderTotal,
    DateTime PlacedAt);

// Shop prices include VAT, so each line total is split into net amount and VAT.
public sealed record CalculatedInvoiceLine(InvoiceLine Line, decimal NetAmount, decimal VatAmount, decimal Total);

public sealed record CalculatedInvoice(
    ValidatedInvoice Invoice,
    IReadOnlyList<CalculatedInvoiceLine> Lines,
    decimal NetAmount,
    decimal VatAmount,
    decimal Total);

public sealed record IssuedInvoice(InvoiceNumber Number, DateTime IssuedAt, CalculatedInvoice Invoice)
{
    public int OrderId => Invoice.Invoice.OrderId;
}

// One invoice per order, so the number is derived from the order: generating it again gives the same number.
public sealed record InvoiceNumber(string Value)
{
    public static InvoiceNumber For(int orderId, DateTime placedAt) => new($"FCT-{placedAt.Year}-{orderId:D6}");

    public override string ToString() => Value;
}

// How the workflow ended when it did not fail. AlreadyIssued is a success: the event was delivered again
// (at-least-once delivery) and the invoice from the first delivery is kept, not duplicated.
public abstract record InvoiceOutcome
{
    private InvoiceOutcome() { }

    public sealed record Issued(IssuedInvoice Invoice) : InvoiceOutcome;
    public sealed record AlreadyIssued(InvoiceNumber Number) : InvoiceOutcome;
}

// Why an invoice cannot be generated. Both are permanent: the same order will never produce a valid invoice.
public abstract record GenerateInvoiceError
{
    private GenerateInvoiceError() { }

    public sealed record InvalidOrder(IReadOnlyList<string> Reasons) : GenerateInvoiceError;
    public sealed record TotalMismatch(decimal OrderTotal, decimal InvoiceTotal) : GenerateInvoiceError;
}
