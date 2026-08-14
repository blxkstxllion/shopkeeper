namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class JoinRequestConfiguration : IEntityTypeConfiguration<JoinRequest>
{
    public void Configure(EntityTypeBuilder<JoinRequest> builder)
    {
        builder.ToTable("JoinRequests");
        builder.HasIndex(r => new { r.BusinessId, r.Status });
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.ReviewedByUser).WithMany().HasForeignKey(r => r.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
