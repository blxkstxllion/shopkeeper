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

        // Partial unique index: at most one still-pending (not yet accepted) invitation per
        // business+email. This is the actual DB-level guard behind InviteEmployeeCommand's
        // "already invited" check - without it, two concurrent invites for the same email have
        // nothing stopping both inserts from succeeding.
        builder.HasIndex(i => new { i.BusinessId, i.Email })
            .IsUnique()
            .HasFilter("\"AcceptedAt\" IS NULL");

        builder.Property(i => i.Email).HasMaxLength(256).IsRequired();
        builder.Property(i => i.Token).HasMaxLength(64).IsRequired();

        builder.HasOne(i => i.Role).WithMany().HasForeignKey(i => i.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(i => i.Branch).WithMany().HasForeignKey(i => i.BranchId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(i => i.InvitedByUser).WithMany().HasForeignKey(i => i.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
