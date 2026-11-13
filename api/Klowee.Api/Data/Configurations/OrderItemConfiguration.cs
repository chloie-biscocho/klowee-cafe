using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.Property(i => i.UnitPrice).HasPrecision(10, 2);
        builder.Property(i => i.LineTotal).HasPrecision(10, 2);

        // The priced row the drink was sold from. Restrict: a menu version that
        // has been sold from must keep its rows (they are only ever soft-deleted).
        builder.HasOne(i => i.MenuVersionItem)
            .WithMany()
            .HasForeignKey(i => i.MenuVersionItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.LoyaltyRedemption)
            .WithMany()
            .HasForeignKey(i => i.LoyaltyRedemptionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.AddOns)
            .WithOne(a => a.OrderItem!)
            .HasForeignKey(a => a.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
