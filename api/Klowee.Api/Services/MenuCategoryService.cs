using Klowee.Api.Common;
using Klowee.Api.Contracts.Menu;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public class MenuCategoryService : IMenuCategoryService
{
    private readonly KloweeDbContext _db;

    public MenuCategoryService(KloweeDbContext db) => _db = db;

    public async Task<IReadOnlyList<MenuCategoryDto>> ListAsync(CancellationToken cancellationToken) =>
        await _db.MenuCategories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new MenuCategoryDto(c.Id, c.Name, c.SortOrder))
            .ToListAsync(cancellationToken);

    public async Task<MenuCategoryDto> CreateAsync(MenuCategoryRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludingId: null, cancellationToken);

        var category = new MenuCategory { Name = name, SortOrder = request.SortOrder };

        _db.MenuCategories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        return new MenuCategoryDto(category.Id, category.Name, category.SortOrder);
    }

    public async Task<MenuCategoryDto> UpdateAsync(Guid id, MenuCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _db.MenuCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Menu category", id);

        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludingId: id, cancellationToken);

        category.Name = name;
        category.SortOrder = request.SortOrder;
        await _db.SaveChangesAsync(cancellationToken);

        return new MenuCategoryDto(category.Id, category.Name, category.SortOrder);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await _db.MenuCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Menu category", id);

        // The query filter already excludes soft-deleted items, so this counts
        // live items only.
        var itemsInUse = await _db.MenuItems.CountAsync(i => i.CategoryId == id, cancellationToken);
        if (itemsInUse > 0)
        {
            throw new ConflictException(
                $"Category '{category.Name}' still has {itemsInUse} menu item(s). Move or delete them first.");
        }

        category.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Mirrors the partial unique index on menu_categories.name so a duplicate
    /// surfaces as a 409 instead of a database error.
    /// </summary>
    private async Task EnsureNameIsFreeAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
    {
        var taken = await _db.MenuCategories.AnyAsync(
            c => c.Name.ToLower() == name.ToLower() && (excludingId == null || c.Id != excludingId),
            cancellationToken);

        if (taken)
        {
            throw new ConflictException($"A menu category named '{name}' already exists.");
        }
    }
}
