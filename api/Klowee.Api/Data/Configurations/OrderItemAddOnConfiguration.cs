using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class OrderItemAddOnConfiguration : IEntityTypeConfiguration<OrderItemAddOn>
{
    public void Configure(EntityTypeBuilder<OrderItemAddOn> builder)
    {
        builder.ToTable("order_item_add_ons");
        builder.Property(a => a.UnitPrice).HasPrecision(10, 2);

        builder.HasOne(a => a.AddOn)
            .WithMany()
            .HasForeignKey(a => a.AddOnId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
