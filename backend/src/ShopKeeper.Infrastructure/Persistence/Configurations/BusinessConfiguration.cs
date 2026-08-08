namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("Businesses");
        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.Property(b => b.LegalName).HasMaxLength(200);
        builder.Property(b => b.Country).HasMaxLength(100).IsRequired();
        builder.Property(b => b.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(b => b.BusinessType).HasConversion<string>().HasMaxLength(50);

        builder.HasMany(b => b.Branches).WithOne(br => br.Business).HasForeignKey(br => br.BusinessId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.Roles).WithOne(r => r.Business).HasForeignKey(r => r.BusinessId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(b => b.BusinessUsers).WithOne(bu => bu.Business).HasForeignKey(bu => bu.BusinessId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Setting).WithOne(s => s.Business).HasForeignKey<BusinessSetting>(s => s.BusinessId).OnDelete(DeleteBehavior.Cascade);
    }
}
