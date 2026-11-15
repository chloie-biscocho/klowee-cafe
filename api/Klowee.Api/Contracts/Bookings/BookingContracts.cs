using System.ComponentModel.DataAnnotations;
using Klowee.Api.Data.Configurations;
using Klowee.Api.Entities;

namespace Klowee.Api.Contracts.Bookings;

/// <summary>
/// What a visitor sends from the homepage. No attributes: the service
/// validates it, after the honeypot check, so a bot that fills `website` gets
/// the same 201 as anyone, whatever else it sent.
/// </summary>
public record PublicBookingRequest
{
    public string? Name { get; init; }
    public string? Contact { get; init; }
    public DateOnly? EventDate { get; init; }
    public string? Venue { get; init; }
    public int? GuestCount { get; init; }
    public Guid? PackageId { get; init; }
    public string? Message { get; init; }

    /// <summary>The honeypot: a field people never see. Anything in it means a bot; nothing is stored.</summary>
    public string? Website { get; init; }
}

/// <summary>The whole public answer: an acknowledgement, never the stored row.</summary>
public record PublicBookingReceipt(bool Received);

/// <summary>A row in the owners' list.</summary>
public record BookingSummaryDto(
    Guid Id,
    DateOnly EventDate,
    string Name,
    string Venue,
    int? GuestCount,
    string? PackageName,
    BookingStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>One request, every field, for the owners.</summary>
public record BookingDto(
    Guid Id,
    string Name,
    string Contact,
    DateOnly EventDate,
    string Venue,
    int? GuestCount,
    Guid? PackageId,
    string? PackageName,
    string? Message,
    BookingStatus Status,
    string? OwnerNotes,
    DateTimeOffset? StatusChangedAt,
    string? StatusChangedByName,
    DateTimeOffset CreatedAt);

/// <summary>
/// The owners' edit: the status and the notes, always both. An empty or
/// missing `ownerNotes` clears them (decision 008: empty means not set), so
/// the admin sends what's in the box every time.
/// </summary>
public record UpdateBookingRequest
{
    [Required]
    public BookingStatus? Status { get; init; }

    [MaxLength(BookingLimits.OwnerNotes)]
    public string? OwnerNotes { get; init; }
}
