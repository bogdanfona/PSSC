using Laborator7_PSSC.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Laborator7_PSSC.Data;

// SQL-first: the tables are created by Laborator4_PSSC/SQL/create-db.sql, then extended by
// Laborator5_PSSC/SQL/update-db.sql and SQL/update-db.sql; this context only maps them.
public class ShoppingCartContext(DbContextOptions<ShoppingCartContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.HasKey(p => p.Id);
            e.Property(p => p.Code).HasMaxLength(20).IsRequired();
            e.HasIndex(p => p.Code).IsUnique();
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Price).HasColumnType("decimal(10,2)");
            e.Property(p => p.QuantityType).HasMaxLength(10).IsRequired();
            e.Property(p => p.Stock);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("Customers");
            e.HasKey(c => c.Id);
            e.Property(c => c.Code).HasMaxLength(20).IsRequired();
            e.HasIndex(c => c.Code).IsUnique();
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("Order");
            e.HasKey(o => o.Id);
            e.Property(o => o.Date).HasColumnType("datetime2");
            e.Property(o => o.DeliveryAddress).HasMaxLength(200).IsRequired();
            e.Property(o => o.Status).HasMaxLength(10).IsRequired();
            e.Property(o => o.PaidAt).HasColumnType("datetime2");
            e.HasOne(o => o.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(o => o.CustomerId);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("OrderItem");
            e.HasKey(i => i.Id);
            e.Property(i => i.Quantity).HasColumnType("decimal(10,3)");
            e.HasIndex(i => new { i.OrderId, i.ProductId }).IsUnique();
            e.HasOne(i => i.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(i => i.OrderId);
            e.HasOne(i => i.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(i => i.ProductId);
        });
    }
}
