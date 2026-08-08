namespace ShopKeeper.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopKeeper.Domain.Entities;

public class BusinessUserConfiguration : IEntityTypeConfiguration<BusinessUser>
{
    public void Configure(EntityTypeBuilder<BusinessUser> builder)
    {
        builder.ToTable("BusinessUsers");
        builder.HasIndex(bu => new { bu.BusinessId, bu.UserId }).IsUnique();
        builder.Property(bu => bu.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(bu => bu.Role)
            .WithMany(r => r.BusinessUsers)
            .HasForeignKey(bu => bu.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(bu => bu.Branch)
            .WithMany()
            .HasForeignKey(bu => bu.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
