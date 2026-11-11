using Laborator8_PSSC.Functional;
using Laborator8_PSSC.Invoicing.Domain.Models;
using Laborator8_PSSC.Invoicing.Domain.Operations;
using Laborator8_PSSC.Invoicing.Domain.Repositories;

namespace Laborator8_PSSC.Invoicing.Domain.Workflows;

// Generates the invoice for a placed order. Impure -> pure -> impure, like the workflows of Labs 5-7:
// read what exists, decide with pure functions, save the result.
public sealed class GenerateInvoiceWorkflow(IInvoicesRepository invoices, TimeProvider clock)
{
    public async Task<Result<InvoiceOutcome, GenerateInvoiceError>> ExecuteAsync(UnvalidatedInvoice command, CancellationToken cancellationToken)
    {
        // Idempotency: the event may arrive more than once; an order is invoiced only the first time.
        if (await invoices.FindByOrderAsync(command.OrderId, cancellationToken) is { } existing)
            return Success(new InvoiceOutcome.AlreadyIssued(existing));

        switch (Generate(command, clock.GetLocalNow().DateTime))
        {
            case Result<IssuedInvoice, GenerateInvoiceError>.Success s:
                // Two deliveries processed at the same time can both get here; the database keeps only one.
                var saved = await invoices.SaveAsync(s.Value, cancellationToken);
                return Success(saved ? new InvoiceOutcome.Issued(s.Value) : new InvoiceOutcome.AlreadyIssued(s.Value.Number));
            case Result<IssuedInvoice, GenerateInvoiceError>.Failure f:
                return new Result<InvoiceOutcome, GenerateInvoiceError>.Failure(f.Error);
            default:
                throw new InvalidOperationException();
        }
    }

    // The pure core: every input is a parameter, so it is tested without a repository or a clock.
    public static Result<IssuedInvoice, GenerateInvoiceError> Generate(UnvalidatedInvoice command, DateTime now) =>
        ValidateInvoiceOperation.Execute(command)
            .Bind(CalculateInvoiceOperation.Execute)
            .Map(calculated => IssueInvoiceOperation.Execute(calculated, now));

    private static Result<InvoiceOutcome, GenerateInvoiceError> Success(InvoiceOutcome outcome) =>
        new Result<InvoiceOutcome, GenerateInvoiceError>.Success(outcome);
}
