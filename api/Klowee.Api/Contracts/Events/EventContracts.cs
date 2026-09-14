using System.ComponentModel.DataAnnotations;
using Klowee.Api.Entities;

namespace Klowee.Api.Contracts.Events;

/// <summary>List row for <c>GET /api/events</c>.</summary>
public record EventSummaryDto(
    Guid Id,
    string Name,
    string Venue,
    DateOnly StartsOn,
    DateOnly EndsOn,
    EventStatus Status,
    bool IsPublished,
    string? CoverPhotoUrl,
    int PhotoCount);

public record EventPhotoDto(Guid Id, string Url, string? Caption, int SortOrder);

public record EventDetailDto(
    Guid Id,
    string Name,
    string Venue,
    string? Address,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string? Description,
    string? CoverPhotoUrl,
    EventStatus Status,
    bool IsPublished,
    IReadOnlyList<EventPhotoDto> Photos);

public record EventRequest
{
    [Required]
    [MaxLength(160)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MaxLength(160)]
    public string Venue { get; init; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; init; }

    [Required]
    public DateOnly StartsOn { get; init; }

    /// <summary>Must be on or after <see cref="StartsOn"/>; a one-day event repeats the date.</summary>
    [Required]
    public DateOnly EndsOn { get; init; }

    [MaxLength(4000)]
    public string? Description { get; init; }

    [MaxLength(2048)]
    [Url]
    public string? CoverPhotoUrl { get; init; }

    [Required]
    [EnumDataType(typeof(EventStatus))]
    public EventStatus Status { get; init; }
}

/// <summary>One entry of the full-replacement payload for <c>PUT /events/{id}/photos</c>.</summary>
public record EventPhotoRequest
{
    [Required]
    [MaxLength(2048)]
    [Url]
    public string Url { get; init; } = string.Empty;

    [MaxLength(300)]
    public string? Caption { get; init; }

    [Range(0, int.MaxValue)]
    public int SortOrder { get; init; }
}
