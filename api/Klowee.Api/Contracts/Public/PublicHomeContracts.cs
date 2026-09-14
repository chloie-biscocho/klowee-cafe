using Klowee.Api.Entities;

namespace Klowee.Api.Contracts.Public;

/// <summary>
/// Everything the marketing site needs to render its one page, in one response.
/// Shapes here are deliberately narrower than the admin DTOs: no ids the site
/// cannot use, no draft or inactive rows, no audit fields.
/// </summary>
public record PublicHomeDto(
    IReadOnlyDictionary<string, string> Settings,
    PublicAnnouncementDto? Announcement,
    PublicEventSummaryDto? NextEvent,
    PublicMenuDto? Menu,
    IReadOnlyList<PublicPackageDto> Packages,
    IReadOnlyList<PublicEventDto> Events);

public record PublicAnnouncementDto(string Text, string? LinkUrl);

public record PublicEventSummaryDto(
    Guid Id,
    string Name,
    string Venue,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string? CoverPhotoUrl);

public record PublicMenuItemDto(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string? PhotoUrl,
    bool IsFeatured);

public record PublicMenuCategoryDto(Guid Id, string Name, IReadOnlyList<PublicMenuItemDto> Items);

public record PublicMenuDto(
    string VersionName,
    DateOnly EffectiveFrom,
    IReadOnlyList<PublicMenuCategoryDto> Categories);

public record PublicPackageDto(
    Guid Id,
    string Name,
    decimal Price,
    string Description,
    string GuestCountNote,
    IReadOnlyList<string> Inclusions);

public record PublicEventPhotoDto(string Url, string? Caption);

public record PublicEventDto(
    Guid Id,
    string Name,
    string Venue,
    DateOnly StartsOn,
    DateOnly EndsOn,
    EventStatus Status,
    string? CoverPhotoUrl,
    string? Description,
    IReadOnlyList<PublicEventPhotoDto> Photos);
