using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.Property(i => i.Name).IsRequired().HasMaxLength(120);
        builder.Property(i => i.Description).IsRequired().HasMaxLength(1000);
        builder.Property(i => i.PhotoUrl).HasMaxLength(2048);
        // Category relationship is configured from MenuCategoryConfiguration.
    }
}
