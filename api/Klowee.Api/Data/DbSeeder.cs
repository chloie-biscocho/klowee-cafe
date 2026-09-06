using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Data;

/// <summary>
/// Inserts baseline development data. Idempotent: each block checks for existing
/// rows before inserting, so running it repeatedly (on every startup) is a no-op.
/// Runs in the Development environment only (see Program.cs).
/// </summary>
public static class DbSeeder
{
    /// <summary>Categories in display order. Kept in one place so the sort orders stay consistent.</summary>
    private static readonly (string Name, int SortOrder)[] Categories =
    [
        ("Coffee", 1),
        ("Matcha", 2),
        ("Milk drinks", 3),
        ("Fruit soda", 4)
    ];

    public static async Task SeedAsync(
        KloweeDbContext db,
        IConfiguration configuration,
        IPasswordHasher<User> passwordHasher,
        ILogger logger)
    {
        await SeedOwnersAsync(db, configuration, passwordHasher, logger);
        await SeedAddOnsAsync(db);
        await SeedCategoriesAsync(db);
        await SeedSiteSettingsAsync(db);
        await SeedSampleMenuVersionAsync(db);
        await MoveStrawberryMilkToMilkDrinksAsync(db);
    }

    /// <summary>
    /// Creates the two owner accounts from user-secrets. There is deliberately no
    /// registration endpoint: owners are provisioned out of band, and the plaintext
    /// password never leaves configuration.
    /// </summary>
    private static async Task SeedOwnersAsync(
        KloweeDbContext db,
        IConfiguration configuration,
        IPasswordHasher<User> passwordHasher,
        ILogger logger)
    {
        var seeded = 0;

        for (var index = 0; index < 2; index++)
        {
            var section = configuration.GetSection($"Seed:Owners:{index}");
            var email = section["Email"];
            var password = section["Password"];
            var displayName = section["DisplayName"];

            if (string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(password)
                || string.IsNullOrWhiteSpace(displayName))
            {
                logger.LogWarning(
                    "Owner seed skipped: Seed:Owners:{Index} needs Email, Password and DisplayName. " +
                    "Set them with: dotnet user-secrets set \"Seed:Owners:{Index}:Email\" \"...\"",
                    index, index);
                continue;
            }

            var normalizedEmail = AuthService.NormalizeEmail(email);

            // Idempotency key: the email.
            if (await db.Users.AnyAsync(u => u.Email == normalizedEmail))
            {
                continue;
            }

            var user = new User
            {
                Email = normalizedEmail,
                DisplayName = displayName.Trim(),
                Role = UserRole.Owner
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);

            db.Users.Add(user);
            seeded++;
            logger.LogInformation("Seeded owner account {Email}.", normalizedEmail);
        }

        if (seeded > 0)
        {
            await db.SaveChangesAsync();
        }
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

    /// <summary>
    /// Upserts each category by name, so adding one to the list above reaches a
    /// database that was already seeded by an earlier run.
    /// </summary>
    private static async Task SeedCategoriesAsync(KloweeDbContext db)
    {
        var existing = await db.MenuCategories.ToDictionaryAsync(c => c.Name);
        var changed = false;

        foreach (var (name, sortOrder) in Categories)
        {
            if (existing.TryGetValue(name, out var category))
            {
                if (category.SortOrder != sortOrder)
                {
                    category.SortOrder = sortOrder;
                    changed = true;
                }

                continue;
            }

            db.MenuCategories.Add(new MenuCategory { Name = name, SortOrder = sortOrder });
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
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
            ("Strawberry Milk", "Milk drinks", 120m),
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

    /// <summary>
    /// Handover 01 filed Strawberry Milk under Coffee for want of a better fit.
    /// Now that "Milk drinks" exists, move it — including in databases seeded
    /// before that category was added.
    /// </summary>
    private static async Task MoveStrawberryMilkToMilkDrinksAsync(KloweeDbContext db)
    {
        var milkDrinks = await db.MenuCategories.FirstOrDefaultAsync(c => c.Name == "Milk drinks");
        if (milkDrinks is null)
        {
            return;
        }

        var strawberryMilk = await db.MenuItems
            .FirstOrDefaultAsync(i => i.Name == "Strawberry Milk" && i.CategoryId != milkDrinks.Id);

        if (strawberryMilk is null)
        {
            return;
        }

        strawberryMilk.CategoryId = milkDrinks.Id;
        await db.SaveChangesAsync();
    }
}
