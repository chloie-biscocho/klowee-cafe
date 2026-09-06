using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class PackageInclusionConfiguration : IEntityTypeConfiguration<PackageInclusion>
{
    public void Configure(EntityTypeBuilder<PackageInclusion> builder)
    {
        builder.Property(i => i.Text).IsRequired().HasMaxLength(300);
    }
}
