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

        // Partial unique index: at most one still-pending join request per user+business. This
        // is the actual DB-level guard behind SubmitJoinRequestForExistingUserCommand's "already
        // pending" check - without it, two concurrent submissions have nothing stopping both
        // inserts from succeeding.
        builder.HasIndex(r => new { r.BusinessId, r.UserId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Pending'");

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.ReviewedByUser).WithMany().HasForeignKey(r => r.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
