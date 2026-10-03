namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasIndex(c => new { c.BusinessId, c.Name });
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CurrentBalance).HasPrecision(18, 2);
    }
}

public class CustomerLedgerEntryConfiguration : IEntityTypeConfiguration<CustomerLedgerEntry>
{
    public void Configure(EntityTypeBuilder<CustomerLedgerEntry> builder)
    {
        builder.ToTable("CustomerLedgerEntries");
        builder.HasIndex(e => new { e.BusinessId, e.CustomerId, e.CreatedAt });
        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.BalanceAfter).HasPrecision(18, 2);
        builder.Property(e => e.ReferenceType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.ReferenceNumber).HasMaxLength(100);
        builder.Property(e => e.Note).HasMaxLength(500);

        // Partial unique index backing RecordCustomerPaymentCommand's idempotency check - same
        // shape as Sale's and Refund's identical indexes.
        builder.HasIndex(e => new { e.BusinessId, e.ClientRequestId })
            .IsUnique()
            .HasFilter("\"ClientRequestId\" IS NOT NULL");

        builder.HasOne(e => e.Customer).WithMany(c => c.LedgerEntries).HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}
