using Laborator8_PSSC.Contracts.Events;
using Laborator8_PSSC.Events;
using Laborator8_PSSC.Functional;
using Laborator8_PSSC.Invoicing.Domain.Models;
using Laborator8_PSSC.Invoicing.Domain.Workflows;
using Microsoft.Extensions.Logging;

namespace Laborator8_PSSC.Invoicing.EventProcessor;

// "Order placed" in the invoicing context: runs the generate invoice workflow.
// Invalid orders are dead-lettered (they will never produce an invoice); a database error is thrown,
// so the message is delivered again; a repeated delivery finds the invoice already issued and completes.
public sealed partial class OrderPlacedHandler(GenerateInvoiceWorkflow workflow, ILogger<OrderPlacedHandler> logger)
    : IEventHandler<OrderPlacedIntegrationEvent>
{
    public async Task<EventProcessingResult> HandleAsync(OrderPlacedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var result = await workflow.ExecuteAsync(integrationEvent.ToCommand(), cancellationToken);
        switch (result)
        {
            case Result<InvoiceOutcome, GenerateInvoiceError>.Success { Value: InvoiceOutcome.Issued issued }:
                var invoice = issued.Invoice.Invoice;
                LogIssued(logger, issued.Invoice.Number.Value, issued.Invoice.OrderId, invoice.NetAmount, invoice.VatAmount, invoice.Total);
                return EventProcessingResult.Completed;

            case Result<InvoiceOutcome, GenerateInvoiceError>.Success { Value: InvoiceOutcome.AlreadyIssued already }:
                LogAlreadyIssued(logger, integrationEvent.OrderId, already.Number.Value);
                return EventProcessingResult.Completed;

            case Result<InvoiceOutcome, GenerateInvoiceError>.Failure f:
                LogRejected(logger, integrationEvent.OrderId, Describe(f.Error));
                return EventProcessingResult.Failed;

            default:
                throw new InvalidOperationException();
        }
    }

    private static string Describe(GenerateInvoiceError error) => error switch
    {
        GenerateInvoiceError.InvalidOrder e => string.Join(" ", e.Reasons),
        GenerateInvoiceError.TotalMismatch e => $"Totalul comenzii ({e.OrderTotal:0.00}) difera de totalul facturii ({e.InvoiceTotal:0.00}).",
        _ => error.ToString(),
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Invoice {Number} issued for order {OrderId}: net {Net:0.00}, VAT {Vat:0.00}, total {Total:0.00} RON")]
    private static partial void LogIssued(ILogger logger, string number, int orderId, decimal net, decimal vat, decimal total);

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderId} already has invoice {Number}; repeated delivery ignored")]
    private static partial void LogAlreadyIssued(ILogger logger, int orderId, string number);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No invoice for order {OrderId}: {Reason}")]
    private static partial void LogRejected(ILogger logger, int orderId, string reason);
}

public static class OrderPlacedMapping
{
    public static UnvalidatedInvoice ToCommand(this OrderPlacedIntegrationEvent e) => new(
        e.OrderId,
        e.CustomerCode,
        e.CustomerName,
        e.DeliveryAddress,
        [.. (e.Lines ?? []).Select(l => new UnvalidatedInvoiceLine(l.ProductCode, l.ProductName, l.Quantity, l.UnitPrice))],
        e.Total,
        e.PlacedAt);
}
