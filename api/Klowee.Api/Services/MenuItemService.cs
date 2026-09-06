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

    public async Task<IReadOnlyList<MenuItemDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken) =>
        await _db.MenuItems
            .AsNoTracking()
            .Where(i => includeArchived || !i.IsArchived)
            .OrderBy(i => i.Category!.SortOrder)
            .ThenBy(i => i.Name)
            .Select(i => new MenuItemDto(
                i.Id, i.Name, i.Description, i.CategoryId, i.Category!.Name, i.PhotoUrl, i.IsArchived))
            .ToListAsync(cancellationToken);

    public async Task<MenuItemDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.MenuItems
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new MenuItemDto(
                i.Id, i.Name, i.Description, i.CategoryId, i.Category!.Name, i.PhotoUrl, i.IsArchived))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw NotFoundException.For("Menu item", id);

    public async Task<MenuItemDto> CreateAsync(MenuItemRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureNameIsFreeAsync(name, excludingId: null, cancellationToken);

        var item = new MenuItem
        {
            Name = name,
            Description = request.Description?.Trim() ?? string.Empty,
            CategoryId = request.CategoryId,
            PhotoUrl = request.PhotoUrl
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

        item.Name = name;
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
