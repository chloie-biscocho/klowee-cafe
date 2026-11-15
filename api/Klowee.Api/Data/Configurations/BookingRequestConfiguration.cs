using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Klowee.Api.Data.Configurations;

public class BookingRequestConfiguration : IEntityTypeConfiguration<BookingRequest>
{
    public void Configure(EntityTypeBuilder<BookingRequest> builder)
    {
        builder.Property(b => b.Name).IsRequired().HasMaxLength(BookingLimits.Name);
        builder.Property(b => b.Contact).IsRequired().HasMaxLength(BookingLimits.Contact);
        builder.Property(b => b.Venue).IsRequired().HasMaxLength(BookingLimits.Venue);
        // Packages are named up to 120 characters.
        builder.Property(b => b.PackageName).HasMaxLength(120);
        builder.Property(b => b.Message).HasMaxLength(BookingLimits.Message);
        builder.Property(b => b.OwnerNotes).HasMaxLength(BookingLimits.OwnerNotes);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(b => b.Status);

        // The package may later be renamed or deleted (soft); the request keeps its copied name.
        builder.HasOne<Package>().WithMany().HasForeignKey(b => b.PackageId).OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>Field lengths and ranges for a booking request, shared by the table and the validation.</summary>
public static class BookingLimits
{
    public const int Name = 100;
    public const int Contact = 200;
    public const int Venue = 200;
    public const int Message = 1000;
    public const int OwnerNotes = 1000;
    public const int MinGuests = 1;
    public const int MaxGuests = 5000;

    /// <summary>How far ahead an event may be asked for.</summary>
    public const int MaxYearsAhead = 2;
}
