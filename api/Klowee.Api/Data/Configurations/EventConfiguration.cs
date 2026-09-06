using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.Property(e => e.Name).IsRequired().HasMaxLength(160);
        builder.Property(e => e.Venue).IsRequired().HasMaxLength(160);
        builder.Property(e => e.Address).HasMaxLength(300);
        builder.Property(e => e.Description).HasMaxLength(4000);
        builder.Property(e => e.CoverPhotoUrl).HasMaxLength(2048);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasMany(e => e.Photos)
            .WithOne(p => p.Event!)
            .HasForeignKey(p => p.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
