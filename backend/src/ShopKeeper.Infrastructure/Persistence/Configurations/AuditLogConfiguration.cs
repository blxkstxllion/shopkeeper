namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasIndex(a => new { a.BusinessId, a.CreatedAt });
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100);
    }
}

public class BusinessSettingConfiguration : IEntityTypeConfiguration<BusinessSetting>
{
    public void Configure(EntityTypeBuilder<BusinessSetting> builder)
    {
        builder.ToTable("BusinessSettings");
        builder.HasIndex(s => s.BusinessId).IsUnique();
        builder.Property(s => s.TaxRatePercent).HasPrecision(5, 2);
    }
}
