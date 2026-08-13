namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class PendingInvitationConfiguration : IEntityTypeConfiguration<PendingInvitation>
{
    public void Configure(EntityTypeBuilder<PendingInvitation> builder)
    {
        builder.ToTable("PendingInvitations");
        builder.HasIndex(i => i.Token).IsUnique();
        builder.HasIndex(i => new { i.BusinessId, i.Email });

        builder.Property(i => i.Email).HasMaxLength(256).IsRequired();
        builder.Property(i => i.Token).HasMaxLength(64).IsRequired();

        builder.HasOne(i => i.Role).WithMany().HasForeignKey(i => i.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Branch).WithMany().HasForeignKey(i => i.BranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(i => i.InvitedByUser).WithMany().HasForeignKey(i => i.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
