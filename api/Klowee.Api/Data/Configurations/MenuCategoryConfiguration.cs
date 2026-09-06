using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> builder)
    {
        builder.Property(c => c.Name).IsRequired().HasMaxLength(80);
        builder.HasIndex(c => c.Name).IsUnique().HasFilter(SoftDelete.Filter);

        builder.HasMany(c => c.Items)
            .WithOne(i => i.Category!)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
