using Klowee.Api.Common;
using Klowee.Api.Contracts.Menu;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public class MenuItemService : IMenuItemService
{
    private readonly KloweeDbContext _db;

    public MenuItemService(KloweeDbContext db) => _db = db;

    public async Task<IReadOnlyList<MenuItemDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var items = await _db.MenuItems
            .AsNoTracking()
            .Where(i => includeArchived || !i.IsArchived)
            .ToListAsync(cancellationToken);
        var categories = await CategoriesAsync(_db, cancellationToken);

        return items
            .OrderBy(i => categories[i.CategoryId].SortOrder)
            .ThenBy(i => i.Name)
            .Select(i => ToDto(i, categories))
            .ToList();
    }

    public async Task<MenuItemDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await _db.MenuItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Menu item", id);
        return ToDto(item, await CategoriesAsync(_db, cancellationToken));
    }

    /// <summary>
    /// Every category's name and order, deleted ones included. An archived item
    /// can sit in a deleted category (handover 14), and joining through the
    /// soft-delete filter would drop it from the list and from old versions.
    /// </summary>
    internal static async Task<Dictionary<Guid, (string Name, int SortOrder)>> CategoriesAsync(KloweeDbContext db, CancellationToken cancellationToken) =>
        await db.MenuCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => (c.Name, c.SortOrder), cancellationToken);

    private static MenuItemDto ToDto(MenuItem i, Dictionary<Guid, (string Name, int SortOrder)> categories) =>
        new(i.Id, i.Name, i.Description, i.CategoryId, categories[i.CategoryId].Name, i.PhotoUrl, i.IsArchived,
            i.LoyaltyMode, i.LoyaltyMode == LoyaltyMode.Club ? i.LoyaltyClubId : null);

    public async Task<MenuItemDto> CreateAsync(MenuItemRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureNameIsFreeAsync(name, excludingId: null, cancellationToken);
        var club = await LoyaltyClubForAsync(request, cancellationToken);

        var item = new MenuItem
        {
            Name = name,
            Description = request.Description?.Trim() ?? string.Empty,
            CategoryId = request.CategoryId,
            PhotoUrl = request.PhotoUrl,
            LoyaltyMode = request.LoyaltyMode,
            LoyaltyClubId = club
        };

        _db.MenuItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(item.Id, cancellationToken);
    }

    public async Task<MenuItemDto> UpdateAsync(Guid id, MenuItemRequest request, CancellationToken cancellationToken)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Menu item", id);

        var name = request.Name.Trim();
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureNameIsFreeAsync(name, excludingId: id, cancellationToken);
        var club = await LoyaltyClubForAsync(request, cancellationToken);

        item.Name = name;
        item.LoyaltyMode = request.LoyaltyMode;
        item.LoyaltyClubId = club;
        item.Description = request.Description?.Trim() ?? string.Empty;
        item.CategoryId = request.CategoryId;
        item.PhotoUrl = request.PhotoUrl;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(item.Id, cancellationToken);
    }

    public async Task<MenuItemDto> SetArchivedAsync(Guid id, bool isArchived, CancellationToken cancellationToken)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Menu item", id);

        // An archived item may sit in a deleted category; it can't come back to life there.
        if (!isArchived && item.IsArchived && !await _db.MenuCategories.AnyAsync(c => c.Id == item.CategoryId, cancellationToken))
        {
            throw new ConflictException($"Menu item '{item.Name}' is in a deleted category. Move it to a category first.");
        }

        item.IsArchived = isArchived;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(item.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await _db.MenuItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Menu item", id);

        // Deleting an item that a version still prices would strand that
        // version's line, so archiving is the way to retire an item.
        var referenceCount = await _db.MenuVersionItems.CountAsync(vi => vi.MenuItemId == id, cancellationToken);
        if (referenceCount > 0)
        {
            throw new ConflictException(
                $"Menu item '{item.Name}' is used by {referenceCount} menu version item(s). Archive it instead.");
        }

        item.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The item's own club: required (and must exist) for mode Club, dropped otherwise.</summary>
    private async Task<Guid?> LoyaltyClubForAsync(MenuItemRequest request, CancellationToken cancellationToken)
    {
        if (request.LoyaltyMode != LoyaltyMode.Club)
        {
            return null;
        }

        if (request.LoyaltyClubId is not { } clubId)
        {
            throw new ValidationFailedException("Choose the loyalty card this item stamps.");
        }

        if (!await _db.LoyaltyClubs.AnyAsync(c => c.Id == clubId, cancellationToken))
        {
            throw new ValidationFailedException($"Loyalty club '{clubId}' does not exist.");
        }

        return clubId;
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var exists = await _db.MenuCategories.AnyAsync(c => c.Id == categoryId, cancellationToken);
        if (!exists)
        {
            throw new ValidationFailedException($"Menu category '{categoryId}' does not exist.");
        }
    }

    /// <summary>Mirrors the partial unique index on menu_items.name.</summary>
    private async Task EnsureNameIsFreeAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
    {
        var taken = await _db.MenuItems.AnyAsync(
            i => i.Name.ToLower() == name.ToLower() && (excludingId == null || i.Id != excludingId),
            cancellationToken);

        if (taken)
        {
            throw new ConflictException($"A menu item named '{name}' already exists.");
        }
    }
}
