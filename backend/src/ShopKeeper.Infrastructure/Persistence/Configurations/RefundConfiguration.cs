namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds");
        builder.HasIndex(r => new { r.BusinessId, r.RefundNumber }).IsUnique();
        builder.Property(r => r.RefundNumber).HasMaxLength(30).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(500).IsRequired();
        builder.Property(r => r.TotalAmount).HasPrecision(18, 2);

        builder.HasOne(r => r.Branch).WithMany().HasForeignKey(r => r.BranchId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Items).WithOne(i => i.Refund).HasForeignKey(i => i.RefundId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefundItemConfiguration : IEntityTypeConfiguration<RefundItem>
{
    public void Configure(EntityTypeBuilder<RefundItem> builder)
    {
        builder.ToTable("RefundItems");
        builder.Property(i => i.Amount).HasPrecision(18, 2);

        builder.HasOne(i => i.SaleItem).WithMany().HasForeignKey(i => i.SaleItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
