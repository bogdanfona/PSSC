using Laborator8_PSSC.Functional;
using Laborator8_PSSC.Invoicing.Domain.Models;
using Laborator8_PSSC.Invoicing.Domain.Repositories;
using Laborator8_PSSC.Invoicing.Domain.Workflows;

namespace Laborator8_PSSC.EventProcessors.Tests;

public class InvoicingWorkflowTests
{
    private static readonly DateTime PlacedAt = new(2026, 11, 11, 10, 0, 0);
    private static readonly DateTime Now = new(2026, 11, 11, 10, 0, 5);

    // 3 x 4.20 + 2 x 6.50 = 25.60, the order paid in the end-to-end run.
    private static UnvalidatedInvoice Order(decimal? total = null, int orderId = 3003) => new(
        orderId, "C-800", "Elena Lab8", "Str. Mare 3, Iasi, 700002",
        [new UnvalidatedInvoiceLine("MER-0001", "Mere", 3, 4.20m), new UnvalidatedInvoiceLine("LAP-0042", "Lapte 1L", 2, 6.50m)],
        total ?? 25.60m, PlacedAt);

    [Fact]
    public void Generate_ValidOrder_SplitsEachLineIntoNetAndVat()
    {
        var invoice = Success(GenerateInvoiceWorkflow.Generate(Order(), Now));

        Assert.Equal("FCT-2026-003003", invoice.Number.Value);
        Assert.Equal(Now, invoice.IssuedAt);
        // 12.60 / 1.21 = 10.41 net + 2.19 VAT; 13.00 / 1.21 = 10.74 net + 2.26 VAT.
        Assert.Equal([10.41m, 10.74m], invoice.Invoice.Lines.Select(l => l.NetAmount));
        Assert.Equal([2.19m, 2.26m], invoice.Invoice.Lines.Select(l => l.VatAmount));
        Assert.Equal(21.15m, invoice.Invoice.NetAmount);
        Assert.Equal(4.45m, invoice.Invoice.VatAmount);
        Assert.Equal(25.60m, invoice.Invoice.Total);
    }

    [Fact]
    public void Generate_TotalDifferentFromLines_FailsWithTotalMismatch()
    {
        var error = Failure(GenerateInvoiceWorkflow.Generate(Order(total: 30m), Now));

        var mismatch = Assert.IsType<GenerateInvoiceError.TotalMismatch>(error);
        Assert.Equal(25.60m, mismatch.InvoiceTotal);
    }

    [Fact]
    public void Generate_InvalidOrder_ReportsEveryProblem()
    {
        var order = new UnvalidatedInvoice(0, " ", null, "", [new UnvalidatedInvoiceLine("", null, 0, -1)], 0, PlacedAt);

        var error = Assert.IsType<GenerateInvoiceError.InvalidOrder>(Failure(GenerateInvoiceWorkflow.Generate(order, Now)));

        Assert.Equal(7, error.Reasons.Count);
    }

    [Fact]
    public async Task ExecuteAsync_NewOrder_SavesTheInvoice()
    {
        var repository = new InMemoryInvoices();

        var outcome = Success(await Workflow(repository).ExecuteAsync(Order(), CancellationToken.None));

        Assert.IsType<InvoiceOutcome.Issued>(outcome);
        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task ExecuteAsync_SameEventTwice_IssuesOnlyOneInvoice()
    {
        var repository = new InMemoryInvoices();
        var workflow = Workflow(repository);

        await workflow.ExecuteAsync(Order(), CancellationToken.None);
        var second = Success(await workflow.ExecuteAsync(Order(), CancellationToken.None));

        Assert.Equal("FCT-2026-003003", Assert.IsType<InvoiceOutcome.AlreadyIssued>(second).Number.Value);
        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentDeliverySavedFirst_ReportsAlreadyIssued()
    {
        // Both deliveries found no invoice; the database accepted only the other one.
        var repository = new InMemoryInvoices { RejectSave = true };

        var outcome = Success(await Workflow(repository).ExecuteAsync(Order(), CancellationToken.None));

        Assert.IsType<InvoiceOutcome.AlreadyIssued>(outcome);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidOrder_SavesNothing()
    {
        var repository = new InMemoryInvoices();

        var result = await Workflow(repository).ExecuteAsync(Order(total: 1m), CancellationToken.None);

        Assert.IsType<Result<InvoiceOutcome, GenerateInvoiceError>.Failure>(result);
        Assert.Empty(repository.Saved);
    }

    private static GenerateInvoiceWorkflow Workflow(InMemoryInvoices repository) => new(repository, new FixedClock(Now));

    private static T Success<T>(Result<T, GenerateInvoiceError> result) =>
        Assert.IsType<Result<T, GenerateInvoiceError>.Success>(result).Value;

    private static GenerateInvoiceError Failure<T>(Result<T, GenerateInvoiceError> result) =>
        Assert.IsType<Result<T, GenerateInvoiceError>.Failure>(result).Error;

    private sealed class InMemoryInvoices : IInvoicesRepository
    {
        public List<IssuedInvoice> Saved { get; } = [];
        public bool RejectSave { get; init; }

        public Task<InvoiceNumber?> FindByOrderAsync(int orderId, CancellationToken cancellationToken) =>
            Task.FromResult(Saved.FirstOrDefault(i => i.OrderId == orderId)?.Number);

        public Task<bool> SaveAsync(IssuedInvoice invoice, CancellationToken cancellationToken)
        {
            if (RejectSave)
                return Task.FromResult(false);
            Saved.Add(invoice);
            return Task.FromResult(true);
        }
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
