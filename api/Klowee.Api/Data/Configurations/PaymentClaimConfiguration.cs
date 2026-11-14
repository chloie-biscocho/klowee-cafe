using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> builder)
    {
        builder.Property(m => m.Channel).HasConversion<string>().HasMaxLength(32);
        builder.Property(m => m.Label).IsRequired().HasMaxLength(40);
        builder.Property(m => m.AccountName).IsRequired().HasMaxLength(120);
        builder.Property(m => m.AccountNumber).HasMaxLength(60);
        builder.Property(m => m.QrUrl).HasMaxLength(2048);
    }
}

public class PaymentClaimConfiguration : IEntityTypeConfiguration<PaymentClaim>
{
    public void Configure(EntityTypeBuilder<PaymentClaim> builder)
    {
        builder.Property(c => c.Amount).HasPrecision(10, 2);
        builder.Property(c => c.Channel).HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(c => c.Reference).IsRequired().HasMaxLength(32);
        builder.Property(c => c.RejectReason).HasMaxLength(500);
        builder.Property(c => c.Note).HasMaxLength(500);
        builder.HasIndex(c => new { c.CustomerId, c.Status });

        builder.HasOne(c => c.Customer).WithMany().HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.PaymentMethod).WithMany().HasForeignKey(c => c.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
    }
}
