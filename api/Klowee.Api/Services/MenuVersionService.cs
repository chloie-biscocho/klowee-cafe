using Klowee.Api.Common;
using Klowee.Api.Contracts.Menu;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public class MenuVersionService : IMenuVersionService
{
    private readonly KloweeDbContext _db;
    private readonly IClock _clock;

    public MenuVersionService(KloweeDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<MenuVersionSummaryDto>> ListAsync(
        MenuContext? context,
        CancellationToken cancellationToken) =>
        await _db.MenuVersions
            .AsNoTracking()
            .Where(v => context == null || v.Context == context)
            .OrderByDescending(v => v.EffectiveFrom)
            .ThenByDescending(v => v.Id)
            .Select(v => new MenuVersionSummaryDto(
                v.Id, v.Name, v.Context, v.EffectiveFrom, v.IsPublished, v.Notes, v.Items.Count))
            .ToListAsync(cancellationToken);

    public async Task<MenuVersionDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var version = await _db.MenuVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Menu version", id);

        var items = await _db.MenuVersionItems
            .AsNoTracking()
            .Where(vi => vi.MenuVersionId == id)
            .OrderBy(vi => vi.MenuItem!.Category!.SortOrder)
            .ThenBy(vi => vi.SortOrder)
            .ThenBy(vi => vi.MenuItem!.Name)
            .Select(vi => new MenuVersionItemDto(
                vi.Id,
                vi.MenuItemId,
                vi.MenuItem!.Name,
                vi.MenuItem!.CategoryId,
                vi.MenuItem!.Category!.Name,
                vi.Price,
                vi.IsAvailable,
                vi.IsFeatured,
                vi.SortOrder))
            .ToListAsync(cancellationToken);

        return new MenuVersionDetailDto(
            version.Id, version.Name, version.Context, version.EffectiveFrom,
            version.IsPublished, version.Notes, items);
    }

    /// <summary>
    /// The "current menu" rule, in one place, so the admin endpoint and the
    /// public home payload can never disagree about which version is live.
    ///
    /// "Current" = the published version for this context whose effective date
    /// has already arrived, most recent first. Future-dated versions can be
    /// prepared and published ahead of time without going live early.
    ///
    /// Id breaks a tie only so the answer is stable; two published versions
    /// sharing one effective date in one context is a data-entry mistake.
    /// (created_at would read better, but SQLite - the test provider - cannot
    /// ORDER BY a timestamptz.)
    ///
    /// "Today" is the date in Cagayan de Oro, not in UTC: a menu effective today
    /// must go live at local midnight, not eight hours later.
    /// </summary>
    public async Task<Guid?> FindCurrentIdAsync(MenuContext context, CancellationToken cancellationToken)
    {
        var today = _clock.Today;

        var id = await _db.MenuVersions
            .AsNoTracking()
            .Where(v => v.Context == context && v.IsPublished && v.EffectiveFrom <= today)
            .OrderByDescending(v => v.EffectiveFrom)
            .ThenByDescending(v => v.Id)
            .Select(v => v.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return id == Guid.Empty ? null : id;
    }

    public async Task<MenuVersionDetailDto> GetCurrentAsync(MenuContext context, CancellationToken cancellationToken)
    {
        var id = await FindCurrentIdAsync(context, cancellationToken)
            ?? throw new NotFoundException(
                $"No published {context} menu is effective as of {_clock.Today:yyyy-MM-dd}.");

        return await GetAsync(id, cancellationToken);
    }

    public async Task<MenuVersionDetailDto> CreateAsync(
        CreateMenuVersionRequest request,
        CancellationToken cancellationToken)
    {
        var version = new MenuVersion
        {
            Name = request.Name.Trim(),
            Context = request.Context,
            EffectiveFrom = request.EffectiveFrom,
            Notes = request.Notes?.Trim(),
            IsPublished = false // new versions always start as a draft
        };

        if (request.CopyFromVersionId is { } sourceId)
        {
            var sourceExists = await _db.MenuVersions.AnyAsync(v => v.Id == sourceId, cancellationToken);
            if (!sourceExists)
            {
                throw NotFoundException.For("Menu version", sourceId);
            }

            var sourceItems = await _db.MenuVersionItems
                .AsNoTracking()
                .Where(vi => vi.MenuVersionId == sourceId)
                .ToListAsync(cancellationToken);

            foreach (var source in sourceItems)
            {
                version.Items.Add(new MenuVersionItem
                {
                    MenuItemId = source.MenuItemId,
                    Price = source.Price,
                    IsAvailable = source.IsAvailable,
                    IsFeatured = source.IsFeatured,
                    SortOrder = source.SortOrder
                });
            }
        }

        _db.MenuVersions.Add(version);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(version.Id, cancellationToken);
    }

    public async Task<MenuVersionDetailDto> UpdateAsync(
        Guid id,
        UpdateMenuVersionRequest request,
        CancellationToken cancellationToken)
    {
        var version = await LoadAsync(id, cancellationToken);
        EnsureNotPublished(version, "edited");

        version.Name = request.Name.Trim();
        version.EffectiveFrom = request.EffectiveFrom;
        version.Notes = request.Notes?.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Idempotent: publishing an already-published version, or unpublishing a
    /// draft, is a 200 with the version unchanged. A PATCH that states a desired
    /// state should not care how many times it is sent — a double-click in the
    /// admin app is not an error.
    /// </summary>
    public async Task<MenuVersionDetailDto> SetPublishedAsync(
        Guid id,
        bool isPublished,
        CancellationToken cancellationToken)
    {
        var version = await LoadAsync(id, cancellationToken);

        version.IsPublished = isPublished;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<MenuVersionDetailDto> ReplaceItemsAsync(
        Guid id,
        IReadOnlyList<MenuVersionItemRequest> items,
        CancellationToken cancellationToken)
    {
        var version = await LoadAsync(id, cancellationToken);
        EnsureNotPublished(version, "changed");

        await ValidateItemsAsync(items, cancellationToken);

        // One transaction, two saves: the existing rows must be soft-deleted and
        // flushed before the replacements are inserted, otherwise re-adding the
        // same menu item would collide with the partial unique index on
        // (menu_version_id, menu_item_id).
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var existing = await _db.MenuVersionItems
            .Where(vi => vi.MenuVersionId == id)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var row in existing)
        {
            row.DeletedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var item in items)
        {
            _db.MenuVersionItems.Add(new MenuVersionItem
            {
                MenuVersionId = id,
                MenuItemId = item.MenuItemId,
                Price = item.Price,
                IsAvailable = item.IsAvailable,
                IsFeatured = item.IsFeatured,
                SortOrder = item.SortOrder
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var version = await LoadAsync(id, cancellationToken);
        EnsureNotPublished(version, "deleted");

        var now = DateTimeOffset.UtcNow;
        version.DeletedAt = now;

        // Soft-delete the lines too, so nothing is left pointing at a version
        // that no longer exists.
        var items = await _db.MenuVersionItems
            .Where(vi => vi.MenuVersionId == id)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.DeletedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<MenuVersion> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.MenuVersions.FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Menu version", id);

    /// <summary>
    /// A published version is the historical record of what was actually sold,
    /// so only the publish flag itself may change after publishing.
    /// </summary>
    private static void EnsureNotPublished(MenuVersion version, string verb)
    {
        if (version.IsPublished)
        {
            throw new ConflictException(
                $"Menu version '{version.Name}' is published and cannot be {verb}. Unpublish it first.");
        }
    }

    private async Task ValidateItemsAsync(
        IReadOnlyList<MenuVersionItemRequest> items,
        CancellationToken cancellationToken)
    {
        var ids = items.Select(i => i.MenuItemId).ToList();

        var duplicate = ids.GroupBy(i => i).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ValidationFailedException($"Menu item '{duplicate.Key}' appears more than once.");
        }

        var known = await _db.MenuItems
            .Where(i => ids.Contains(i.Id))
            .Select(i => new { i.Id, i.IsArchived })
            .ToListAsync(cancellationToken);

        var missing = ids.Except(known.Select(k => k.Id)).ToList();
        if (missing.Count > 0)
        {
            throw new ValidationFailedException(
                $"Unknown menu item(s): {string.Join(", ", missing)}.");
        }

        var archived = known.Where(k => k.IsArchived).Select(k => k.Id).ToList();
        if (archived.Count > 0)
        {
            throw new ValidationFailedException(
                $"Archived menu item(s) cannot be priced: {string.Join(", ", archived)}.");
        }
    }
}
