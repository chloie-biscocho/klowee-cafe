using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.Property(i => i.Name).IsRequired().HasMaxLength(120);
        builder.HasIndex(i => i.Name).IsUnique().HasFilter(SoftDelete.Filter);
        builder.Property(i => i.Description).IsRequired().HasMaxLength(1000);
        builder.Property(i => i.PhotoUrl).HasMaxLength(2048);
        builder.Property(i => i.LoyaltyMode).HasConversion<string>().HasMaxLength(32);
        builder.HasOne(i => i.LoyaltyClub)
            .WithMany()
            .HasForeignKey(i => i.LoyaltyClubId)
            .OnDelete(DeleteBehavior.Restrict);

        // Category relationship is configured from MenuCategoryConfiguration.
    }
}
