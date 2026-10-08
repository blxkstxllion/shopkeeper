namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class LocalAppSettingsConfiguration : IEntityTypeConfiguration<LocalAppSettings>
{
    public void Configure(EntityTypeBuilder<LocalAppSettings> builder)
    {
        builder.ToTable("LocalAppSettings");
        builder.Property(s => s.StoreName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.PinHash).IsRequired();
    }
}
