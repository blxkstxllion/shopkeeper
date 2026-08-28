namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class PaystackWebhookEventConfiguration : IEntityTypeConfiguration<PaystackWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaystackWebhookEvent> builder)
    {
        builder.ToTable("PaystackWebhookEvents");
        builder.Property(e => e.RawPayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(50).IsRequired();
        builder.HasIndex(e => e.RawPayloadHash).IsUnique();
    }
}
