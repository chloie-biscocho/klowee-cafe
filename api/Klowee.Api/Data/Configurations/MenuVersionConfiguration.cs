using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class MenuVersionConfiguration : IEntityTypeConfiguration<MenuVersion>
{
    public void Configure(EntityTypeBuilder<MenuVersion> builder)
    {
        builder.Property(v => v.Name).IsRequired().HasMaxLength(120);
        builder.Property(v => v.Context).HasConversion<string>().HasMaxLength(32);
        builder.Property(v => v.Notes).HasMaxLength(2000);

        builder.HasMany(v => v.Items)
            .WithOne(i => i.MenuVersion!)
            .HasForeignKey(i => i.MenuVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
