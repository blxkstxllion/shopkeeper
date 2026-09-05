namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("IdempotencyKeys");

        // Every row here was created for a request that actually carried a ClientRequestId
        // (see IdempotencyBehavior), so a plain unique index is enough - unlike Sales' own
        // ClientRequestId column (present on every row, null for normal online sales), there's
        // no null case to filter out here.
        builder.HasIndex(k => new { k.BusinessId, k.ClientRequestId }).IsUnique();

        builder.Property(k => k.RequestType).HasMaxLength(200).IsRequired();
        builder.Property(k => k.ResponseJson).IsRequired();
    }
}
