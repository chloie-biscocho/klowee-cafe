using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(c => c.Name).IsRequired().HasMaxLength(120);
        builder.Property(c => c.Nickname).HasMaxLength(80);
        builder.Property(c => c.Notes).HasMaxLength(1000);

        builder.HasIndex(c => c.Name).IsUnique().HasFilter(SoftDelete.Filter);
    }
}
