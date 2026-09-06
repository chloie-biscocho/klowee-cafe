using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.Property(p => p.Name).IsRequired().HasMaxLength(120);
        builder.Property(p => p.Price).HasPrecision(10, 2);
        builder.Property(p => p.Description).IsRequired().HasMaxLength(2000);
        builder.Property(p => p.GuestCountNote).IsRequired().HasMaxLength(120);

        builder.HasMany(p => p.Inclusions)
            .WithOne(i => i.Package!)
            .HasForeignKey(i => i.PackageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
