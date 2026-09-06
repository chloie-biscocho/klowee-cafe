using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class AddOnConfiguration : IEntityTypeConfiguration<AddOn>
{
    public void Configure(EntityTypeBuilder<AddOn> builder)
    {
        builder.Property(a => a.Name).IsRequired().HasMaxLength(80);
        builder.HasIndex(a => a.Name).IsUnique().HasFilter(SoftDelete.Filter);
        builder.Property(a => a.Price).HasPrecision(10, 2);
    }
}
