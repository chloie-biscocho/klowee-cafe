using Klowee.Api.Entities.Common;

namespace Klowee.Api.Entities;

/// <summary>Where the owners are with a booking request. Starts New; any status can move to any other.</summary>
public enum BookingStatus
{
    New,
    Contacted,
    Confirmed,
    Declined
}

/// <summary>
/// A visitor asking for the cart at their event, sent from the homepage's
/// Book section (handover 20, decision 023). A request, not a booking: the
/// owners reply by phone or chat and decide; nothing is confirmed or paid
/// through the site. Only the owners can read it.
/// </summary>
public class BookingRequest : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>How to reach them, as they typed it: a mobile number, Messenger name, Instagram handle or email.</summary>
    public string Contact { get; set; } = string.Empty;

    public DateOnly EventDate { get; set; }
    public string Venue { get; set; } = string.Empty;
    public int? GuestCount { get; set; }

    /// <summary>The package they picked, if any.</summary>
    public Guid? PackageId { get; set; }

    /// <summary>The package's name when the request was sent, so a later rename or delete doesn't change it.</summary>
    public string? PackageName { get; set; }

    public string? Message { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.New;

    /// <summary>The owners' own notes; never shown to the visitor.</summary>
    public string? OwnerNotes { get; set; }

    public DateTimeOffset? StatusChangedAt { get; set; }

    /// <summary>The owner (user id) who last changed the status.</summary>
    public Guid? StatusChangedBy { get; set; }
}
