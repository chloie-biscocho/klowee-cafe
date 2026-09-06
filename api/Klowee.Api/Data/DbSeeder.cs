using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Data;

/// <summary>
/// Inserts baseline development data. Idempotent: each block checks for existing
/// rows before inserting, so running it repeatedly (on every startup) is a no-op.
/// Runs in the Development environment only (see Program.cs).
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(KloweeDbContext db)
    {
        await SeedAddOnsAsync(db);
        await SeedCategoriesAsync(db);
        await SeedSiteSettingsAsync(db);
        await SeedSampleMenuVersionAsync(db);
    }

    private static async Task SeedAddOnsAsync(KloweeDbContext db)
    {
        if (await db.AddOns.AnyAsync())
        {
            return;
        }

        db.AddOns.AddRange(
            new AddOn { Name = "Extra shot", Price = 15m, IsActive = true },
            new AddOn { Name = "Oat milk", Price = 30m, IsActive = true });

        await db.SaveChangesAsync();
    }

    private static async Task SeedCategoriesAsync(KloweeDbContext db)
    {
        if (await db.MenuCategories.AnyAsync())
        {
            return;
        }

        db.MenuCategories.AddRange(
            new MenuCategory { Name = "Coffee", SortOrder = 1 },
            new MenuCategory { Name = "Matcha", SortOrder = 2 },
            new MenuCategory { Name = "Fruit soda", SortOrder = 3 });

        await db.SaveChangesAsync();
    }

    private static async Task SeedSiteSettingsAsync(KloweeDbContext db)
    {
        if (await db.SiteSettings.AnyAsync())
        {
            return;
        }

        var settings = new (string Key, string Value)[]
        {
            ("hero_heading", "smol pop-up cafe, big on matcha."),
            ("hero_body", "Handcrafted coffee, matcha, and fruit sodas — rolling into your events around Cagayan de Oro."),
            ("story_heading", "Our story"),
            ("story_body", "Klowee Cafe started as a two-friend passion project: a little cart, good beans, and a lot of matcha. We pop up at offices and events across Cagayan de Oro."),
            ("ticker_fallback", "Now booking pop-ups around Cagayan de Oro — say hi!"),
            ("instagram_url", "https://instagram.com/kloweecafe"),
            ("facebook_url", "https://facebook.com/kloweecafe"),
            ("contact_email", "hello@kloweecafe.com"),
        };

        db.SiteSettings.AddRange(settings.Select(s => new SiteSetting { Key = s.Key, Value = s.Value }));
        await db.SaveChangesAsync();
    }

    private static async Task SeedSampleMenuVersionAsync(KloweeDbContext db)
    {
        if (await db.MenuVersions.AnyAsync())
        {
            return;
        }

        var categories = await db.MenuCategories.ToDictionaryAsync(c => c.Name, c => c);

        // (item name, category, price) for the sample pop-up menu.
        var lineItems = new (string Name, string Category, decimal Price)[]
        {
            ("Americano", "Coffee", 110m),
            ("Cafe Latte", "Coffee", 130m),
            ("Spanish Latte", "Coffee", 150m),
            ("Hazelnut Latte", "Coffee", 130m),
            ("Cinnamon Cloud", "Coffee", 160m),
            ("Ube Latte", "Coffee", 160m),
            ("Oat Biscoff Latte", "Coffee", 140m),
            ("Strawberry Milk", "Coffee", 120m),
            ("Matcha Latte", "Matcha", 150m),
            ("Berry Matcha", "Matcha", 140m),
            ("Dirty Matcha", "Matcha", 140m),
            ("Strawberry Swoon", "Fruit soda", 99m),
            ("Blueberry Fizz", "Fruit soda", 99m),
        };

        var version = new MenuVersion
        {
            Name = "April 2026 pop-up",
            Context = MenuContext.PopUp,
            EffectiveFrom = new DateOnly(2026, 4, 1),
            IsPublished = true,
            Notes = "Seeded sample menu."
        };

        var sortOrder = 0;
        foreach (var (name, category, price) in lineItems)
        {
            var item = new MenuItem
            {
                Name = name,
                Description = string.Empty,
                Category = categories[category],
                IsArchived = false
            };

            version.Items.Add(new MenuVersionItem
            {
                MenuItem = item,
                Price = price,
                IsAvailable = true,
                IsFeatured = false,
                SortOrder = sortOrder++
            });
        }

        db.MenuVersions.Add(version);
        await db.SaveChangesAsync();
    }
}
