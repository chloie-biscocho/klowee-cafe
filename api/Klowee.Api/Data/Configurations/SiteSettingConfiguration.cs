using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class SiteSettingConfiguration : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> builder)
    {
        builder.Property(s => s.Key).IsRequired().HasMaxLength(120);
        builder.HasIndex(s => s.Key).IsUnique();

        builder.Property(s => s.Value).IsRequired().HasColumnType("text");
    }
}
