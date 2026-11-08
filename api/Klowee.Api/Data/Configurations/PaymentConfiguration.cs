using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(p => p.Amount).HasPrecision(10, 2);
        builder.Property(p => p.Channel).HasConversion<string>().HasMaxLength(32);
        builder.Property(p => p.Reference).HasMaxLength(120);
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.HasIndex(p => p.PaidOn);
        // Order relationship is configured from OrderConfiguration.
    }
}
