using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

/// <summary>
/// Which club a drink stamps (decision 020): the item's own setting when it
/// says Club or None, otherwise its category's club. Built once per operation
/// from every item and category, deleted and archived ones included, so a
/// retired drink on an old order still resolves.
/// </summary>
public sealed class LoyaltyClubMap
{
    private readonly Dictionary<Guid, Guid> _itemOfVersionItem;
    private readonly Dictionary<Guid, (LoyaltyMode Mode, Guid? Club, Guid CategoryId)> _items;
    private readonly Dictionary<Guid, Guid?> _categories;

    private LoyaltyClubMap(
        Dictionary<Guid, Guid> itemOfVersionItem,
        Dictionary<Guid, (LoyaltyMode, Guid?, Guid)> items,
        Dictionary<Guid, Guid?> categories)
    {
        _itemOfVersionItem = itemOfVersionItem;
        _items = items;
        _categories = categories;
    }

    /// <summary>Rule 1, on its own: Club → the item's club, None → none, Inherit → the category's.</summary>
    public static Guid? Resolve(LoyaltyMode mode, Guid? itemClub, Guid? categoryClub) => mode switch
    {
        LoyaltyMode.Club => itemClub,
        LoyaltyMode.None => null,
        _ => categoryClub
    };

    /// <summary>The club a menu item's drinks stamp, or null.</summary>
    public Guid? ForItem(Guid menuItemId) =>
        _items.TryGetValue(menuItemId, out var item)
            ? Resolve(item.Mode, item.Club, _categories.GetValueOrDefault(item.CategoryId))
            : null;

    /// <summary>The club a priced row (an order line's drink) stamps, or null.</summary>
    public Guid? ForVersionItem(Guid menuVersionItemId) =>
        _itemOfVersionItem.TryGetValue(menuVersionItemId, out var itemId) ? ForItem(itemId) : null;

    /// <param name="versionItemIds">The priced rows to resolve; none needed when only items are resolved.</param>
    public static async Task<LoyaltyClubMap> LoadAsync(
        KloweeDbContext db,
        IReadOnlyCollection<Guid> versionItemIds,
        CancellationToken cancellationToken)
    {
        var itemOfVersionItem = versionItemIds.Count == 0
            ? new Dictionary<Guid, Guid>()
            : await db.MenuVersionItems
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(vi => versionItemIds.Contains(vi.Id))
                .ToDictionaryAsync(vi => vi.Id, vi => vi.MenuItemId, cancellationToken);

        var items = await db.MenuItems
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(i => new { i.Id, i.LoyaltyMode, i.LoyaltyClubId, i.CategoryId })
            .ToDictionaryAsync(i => i.Id, i => (i.LoyaltyMode, i.LoyaltyClubId, i.CategoryId), cancellationToken);

        var categories = await db.MenuCategories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.LoyaltyClubId, cancellationToken);

        return new LoyaltyClubMap(itemOfVersionItem, items, categories);
    }
}
