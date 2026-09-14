using Klowee.Api.Contracts.Public;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

/// <summary>
/// Builds the single payload the marketing site reads.
///
/// It <b>composes from the other services</b> for settings, the current
/// announcement, the next event and the active packages: those four are exactly
/// the rules those services already own, and their DTOs carry every field the
/// site needs. Duplicating the queries here is how the admin app and the public
/// site would drift apart about what "current" means.
///
/// It <b>queries the DbContext directly</b> for the menu and the event list,
/// because those two shapes differ: the public menu needs each item's
/// description and photo, which <c>MenuVersionItemDto</c> deliberately does not
/// carry (the admin editor prices items, it does not display them), and the
/// public event list needs photos inline, which <c>EventSummaryDto</c> does not.
/// Widening two admin contracts to serve one consumer is the worse trade. The
/// rules are still not duplicated: which version is live comes from
/// <see cref="IMenuVersionService.FindCurrentIdAsync"/>, and "still upcoming"
/// from <see cref="IClock"/>.
/// </summary>
public class PublicHomeService : IPublicHomeService
{
    private readonly KloweeDbContext _db;
    private readonly IClock _clock;
    private readonly ISiteSettingService _settings;
    private readonly IAnnouncementService _announcements;
    private readonly IEventService _events;
    private readonly IPackageService _packages;
    private readonly IMenuVersionService _menuVersions;

    public PublicHomeService(
        KloweeDbContext db,
        IClock clock,
        ISiteSettingService settings,
        IAnnouncementService announcements,
        IEventService events,
        IPackageService packages,
        IMenuVersionService menuVersions)
    {
        _db = db;
        _clock = clock;
        _settings = settings;
        _announcements = announcements;
        _events = events;
        _packages = packages;
        _menuVersions = menuVersions;
    }

    public async Task<PublicHomeDto> GetHomeAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.ListAsync(cancellationToken);
        var announcement = await _announcements.GetCurrentAsync(cancellationToken);
        var nextEvent = await _events.GetNextAsync(cancellationToken);
        var packages = await _packages.ListAsync(includeInactive: false, cancellationToken);

        return new PublicHomeDto(
            Settings: settings
                .Where(s => SiteSettingService.KnownKeys.Contains(s.Key))
                .ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal),

            Announcement: announcement is null
                ? null
                : new PublicAnnouncementDto(announcement.Text, announcement.LinkUrl),

            NextEvent: nextEvent is null
                ? null
                : new PublicEventSummaryDto(
                    nextEvent.Id, nextEvent.Name, nextEvent.Venue,
                    nextEvent.StartsOn, nextEvent.EndsOn, nextEvent.CoverPhotoUrl),

            Menu: await GetMenuAsync(cancellationToken),

            Packages: packages
                .Select(p => new PublicPackageDto(
                    p.Id, p.Name, p.Price, p.Description, p.GuestCountNote,
                    p.Inclusions.Select(i => i.Text).ToList()))
                .ToList(),

            Events: await GetEventsAsync(cancellationToken));
    }

    /// <summary>The live pop-up menu, grouped by category, available items only.</summary>
    private async Task<PublicMenuDto?> GetMenuAsync(CancellationToken cancellationToken)
    {
        var versionId = await _menuVersions.FindCurrentIdAsync(MenuContext.PopUp, cancellationToken);
        if (versionId is null)
        {
            return null;
        }

        var version = await _db.MenuVersions
            .AsNoTracking()
            .Where(v => v.Id == versionId)
            .Select(v => new { v.Name, v.EffectiveFrom })
            .FirstOrDefaultAsync(cancellationToken);

        if (version is null)
        {
            return null;
        }

        var rows = await _db.MenuVersionItems
            .AsNoTracking()
            .Where(vi => vi.MenuVersionId == versionId && vi.IsAvailable)
            .OrderBy(vi => vi.MenuItem!.Category!.SortOrder)
            .ThenBy(vi => vi.SortOrder)
            .ThenBy(vi => vi.MenuItem!.Name)
            .Select(vi => new
            {
                CategoryId = vi.MenuItem!.CategoryId,
                CategoryName = vi.MenuItem!.Category!.Name,
                CategorySortOrder = vi.MenuItem!.Category!.SortOrder,
                Item = new PublicMenuItemDto(
                    vi.MenuItemId,
                    vi.MenuItem!.Name,
                    vi.MenuItem!.Description,
                    vi.Price,
                    vi.MenuItem!.PhotoUrl,
                    vi.IsFeatured)
            })
            .ToListAsync(cancellationToken);

        var categories = rows
            .GroupBy(row => new { row.CategoryId, row.CategoryName, row.CategorySortOrder })
            .OrderBy(group => group.Key.CategorySortOrder)
            .ThenBy(group => group.Key.CategoryName)
            .Select(group => new PublicMenuCategoryDto(
                group.Key.CategoryId,
                group.Key.CategoryName,
                group.Select(row => row.Item).ToList()))
            .ToList();

        return new PublicMenuDto(version.Name, version.EffectiveFrom, categories);
    }

    /// <summary>Published events: what is still to come first, then the most recent past.</summary>
    private async Task<IReadOnlyList<PublicEventDto>> GetEventsAsync(CancellationToken cancellationToken)
    {
        var today = _clock.Today;

        var events = await _db.Events
            .AsNoTracking()
            .Where(e => e.IsPublished)
            .Select(e => new
            {
                IsUpcoming = e.EndsOn >= today,
                Dto = new PublicEventDto(
                    e.Id, e.Name, e.Venue, e.StartsOn, e.EndsOn, e.Status,
                    e.CoverPhotoUrl, e.Description,
                    e.Photos
                        .OrderBy(p => p.SortOrder)
                        .Select(p => new PublicEventPhotoDto(p.Url, p.Caption))
                        .ToList())
            })
            .ToListAsync(cancellationToken);

        return events
            .OrderByDescending(e => e.IsUpcoming)
            // Soonest first while it is still ahead; most recent first once past.
            .ThenBy(e => e.IsUpcoming ? e.Dto.StartsOn : DateOnly.MaxValue)
            .ThenByDescending(e => e.Dto.StartsOn)
            .Select(e => e.Dto)
            .ToList();
    }
}
