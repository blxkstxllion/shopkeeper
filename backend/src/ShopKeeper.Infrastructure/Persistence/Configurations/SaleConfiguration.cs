namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("Sales");
        builder.HasIndex(s => new { s.BusinessId, s.SaleNumber }).IsUnique();
        builder.HasIndex(s => new { s.BusinessId, s.BranchId, s.CreatedAt });

        // Partial unique index: an offline-queued sale's client-generated key can only ever
        // back one real Sale per business. Null (today's normal online-created sales) is never
        // compared as equal to itself under a partial index, so any number of them can coexist.
        builder.HasIndex(s => new { s.BusinessId, s.ClientRequestId })
            .IsUnique()
            .HasFilter("\"ClientRequestId\" IS NOT NULL");

        builder.Property(s => s.SaleNumber).HasMaxLength(30).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.VoidReason).HasMaxLength(500);

        foreach (var money in new[] { nameof(Sale.Subtotal), nameof(Sale.DiscountAmount), nameof(Sale.TaxAmount), nameof(Sale.Total), nameof(Sale.TotalCost), nameof(Sale.GrossProfit) })
        {
            builder.Property(money).HasPrecision(18, 2);
        }

        builder.HasOne(s => s.Branch).WithMany().HasForeignKey(s => s.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.CashierUser).WithMany().HasForeignKey(s => s.CashierUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Customer).WithMany(c => c.Sales).HasForeignKey(s => s.CustomerId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(s => s.Items).WithOne(i => i.Sale).HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Payments).WithOne(p => p.Sale).HasForeignKey(p => p.SaleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Refunds).WithOne(r => r.Sale).HasForeignKey(r => r.SaleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SaleItems");
        builder.Property(i => i.ProductNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(i => i.SkuSnapshot).HasMaxLength(50).IsRequired();

        foreach (var money in new[] { nameof(SaleItem.UnitPrice), nameof(SaleItem.UnitCost), nameof(SaleItem.DiscountAmount), nameof(SaleItem.LineRevenue), nameof(SaleItem.LineCost), nameof(SaleItem.LineProfit) })
        {
            builder.Property(money).HasPrecision(18, 2);
        }

        builder.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasIndex(p => p.SaleId);
        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Amount).HasPrecision(18, 2);
        builder.Property(p => p.ReferenceNumber).HasMaxLength(100);
    }
}
