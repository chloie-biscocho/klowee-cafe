using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.Context).HasConversion<string>().HasMaxLength(32);
        builder.Property(o => o.DiscountType).HasConversion<string>().HasMaxLength(32);
        builder.Property(o => o.Notes).HasMaxLength(1000);

        builder.Property(o => o.DiscountValue).HasPrecision(10, 2);
        builder.Property(o => o.Subtotal).HasPrecision(10, 2);
        builder.Property(o => o.DiscountAmount).HasPrecision(10, 2);
        builder.Property(o => o.Total).HasPrecision(10, 2);

        builder.HasIndex(o => o.OrderedOn);

        // Customers are never hard-deleted (decision 003), and a customer with
        // orders cannot even be soft-deleted, so Restrict is only a backstop.
        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Payer)
            .WithMany()
            .HasForeignKey(o => o.PayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items)
            .WithOne(i => i.Order!)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Payments)
            .WithOne(p => p.Order!)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
