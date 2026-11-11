using Microsoft.EntityFrameworkCore;

namespace Laborator8_PSSC.Invoicing.EventProcessor.Data;

// SQL-first, like Labs 4-7: the tables come from SQL/update-db.sql; this context only maps them.
// The invoicing context owns the "invoicing" schema and does not read the shop's tables.
public sealed class InvoicingContext(DbContextOptions<InvoicingContext> options) : DbContext(options)
{
    public DbSet<InvoiceEntity> Invoices => Set<InvoiceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceEntity>(e =>
        {
            e.ToTable("Invoices", "invoicing");
            e.HasKey(i => i.Id);
            e.Property(i => i.Number).HasMaxLength(30);
            e.HasIndex(i => i.Number).IsUnique();
            e.HasIndex(i => i.OrderId).IsUnique();
            e.Property(i => i.CustomerCode).HasMaxLength(20);
            e.Property(i => i.CustomerName).HasMaxLength(100);
            e.Property(i => i.BillingAddress).HasMaxLength(200);
            e.Property(i => i.NetAmount).HasPrecision(12, 2);
            e.Property(i => i.VatAmount).HasPrecision(12, 2);
            e.Property(i => i.Total).HasPrecision(12, 2);
            e.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId);
        });

        modelBuilder.Entity<InvoiceLineEntity>(e =>
        {
            e.ToTable("InvoiceLines", "invoicing");
            e.HasKey(l => l.Id);
            e.Property(l => l.ProductCode).HasMaxLength(20);
            e.Property(l => l.ProductName).HasMaxLength(100);
            e.Property(l => l.Quantity).HasPrecision(10, 3);
            e.Property(l => l.UnitPrice).HasPrecision(10, 2);
            e.Property(l => l.NetAmount).HasPrecision(12, 2);
            e.Property(l => l.VatAmount).HasPrecision(12, 2);
            e.Property(l => l.Total).HasPrecision(12, 2);
        });
    }
}

public sealed class InvoiceEntity
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int OrderId { get; set; }
    public string CustomerCode { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string BillingAddress { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public List<InvoiceLineEntity> Lines { get; set; } = [];
}

public sealed class InvoiceLineEntity
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal NetAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
}
