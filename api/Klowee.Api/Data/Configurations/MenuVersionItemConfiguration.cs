using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class MenuVersionItemConfiguration : IEntityTypeConfiguration<MenuVersionItem>
{
    public void Configure(EntityTypeBuilder<MenuVersionItem> builder)
    {
        builder.Property(i => i.Price).HasPrecision(10, 2);

        builder.HasIndex(i => new { i.MenuVersionId, i.MenuItemId }).IsUnique();

        builder.HasOne(i => i.MenuItem)
            .WithMany(m => m.VersionItems)
            .HasForeignKey(i => i.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);
        // MenuVersion relationship is configured from MenuVersionConfiguration.
    }
}
