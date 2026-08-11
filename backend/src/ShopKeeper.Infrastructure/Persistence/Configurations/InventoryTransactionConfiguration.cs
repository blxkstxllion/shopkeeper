namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        builder.ToTable("InventoryTransactions");
        builder.HasIndex(t => new { t.BusinessId, t.ProductId, t.BranchId, t.CreatedAt });
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Reason).HasMaxLength(500).IsRequired();
        builder.Property(t => t.ReferenceType).HasMaxLength(50);

        builder.HasOne(t => t.Product).WithMany().HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Branch).WithMany().HasForeignKey(t => t.BranchId).OnDelete(DeleteBehavior.Restrict);
    }
}
