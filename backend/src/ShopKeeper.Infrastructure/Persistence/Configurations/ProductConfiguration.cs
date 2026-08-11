namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategories");
        builder.HasIndex(c => new { c.BusinessId, c.Name });
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasIndex(p => new { p.BusinessId, p.Sku }).IsUnique();
        builder.HasIndex(p => new { p.BusinessId, p.Barcode });
        builder.HasIndex(p => new { p.BusinessId, p.Name });

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Sku).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Barcode).HasMaxLength(50);
        builder.Property(p => p.SellingPrice).HasPrecision(18, 2);
        builder.Property(p => p.CostPrice).HasPrecision(18, 2);

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ProductStockConfiguration : IEntityTypeConfiguration<ProductStock>
{
    public void Configure(EntityTypeBuilder<ProductStock> builder)
    {
        builder.ToTable("ProductStocks");
        builder.HasIndex(s => new { s.ProductId, s.BranchId }).IsUnique();

        builder.HasOne(s => s.Product)
            .WithMany(p => p.StockByBranch)
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Branch)
            .WithMany()
            .HasForeignKey(s => s.BranchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
