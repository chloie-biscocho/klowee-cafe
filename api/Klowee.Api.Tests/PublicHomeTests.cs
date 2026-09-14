using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Public;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Tests;

public class PublicHomeTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public PublicHomeTests(KloweeApiFactory factory) => _factory = factory;

    private static readonly DateOnly Today = new(2026, 9, 14);

    [Fact]
    public async Task Home_IsAnonymousAndCacheableForAMinute()
    {
        var client = await _factory.CreateAnonymousClientAsync();

        var response = await client.GetAsync("/api/public/home");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=60", response.Headers.CacheControl?.ToString());
    }

    /// <summary>
    /// The one rule the public payload has to get right: nothing a visitor should
    /// not see. Drafts, inactive rows, sold-out items and finished announcements
    /// all live in the same tables as the things that should show.
    /// </summary>
    [Fact]
    public async Task Home_ExcludesEverythingThatIsNotLive()
    {
        _factory.Clock.Today = Today;
        await SeedAsync();

        var client = await _factory.CreateAnonymousClientAsync();
        var home = await client.GetFromJsonAsync<PublicHomeDto>("/api/public/home", KloweeApiFactory.Json);

        Assert.NotNull(home);

        // Events: published only.
        Assert.Contains(home!.Events, e => e.Name == "Published market");
        Assert.DoesNotContain(home.Events, e => e.Name == "Unpublished market");

        // Packages: active only.
        Assert.Contains(home.Packages, p => p.Name == "Live package");
        Assert.DoesNotContain(home.Packages, p => p.Name == "Retired package");

        // Menu: available items only, from the version that is effective today.
        Assert.NotNull(home.Menu);
        Assert.Equal("Live pop-up menu", home.Menu!.VersionName);

        var menuItems = home.Menu.Categories.SelectMany(c => c.Items).ToList();
        Assert.Contains(menuItems, i => i.Name == "Available Latte");
        Assert.DoesNotContain(menuItems, i => i.Name == "Sold Out Latte");

        // ...and not from the draft version, nor the future-dated one.
        Assert.DoesNotContain(menuItems, i => i.Name == "Draft Only Latte");

        // Announcement: the running one, not the expired one.
        Assert.NotNull(home.Announcement);
        Assert.Equal("Running now", home.Announcement!.Text);

        // Next event: published, Upcoming, not yet over.
        Assert.NotNull(home.NextEvent);
        Assert.Equal("Published market", home.NextEvent!.Name);

        // Settings arrive as a key/value map of the known keys.
        Assert.Equal("smol pop-up cafe", home.Settings["hero_heading"]);
    }

    [Fact]
    public async Task Home_PutsUpcomingEventsBeforePastOnes()
    {
        _factory.Clock.Today = Today;
        await SeedAsync();

        var client = await _factory.CreateAnonymousClientAsync();
        var home = await client.GetFromJsonAsync<PublicHomeDto>("/api/public/home", KloweeApiFactory.Json);

        var names = home!.Events.Select(e => e.Name).ToList();

        Assert.Equal("Published market", names[0]);
        Assert.Equal("Recent past market", names[1]);
        Assert.Equal("Older past market", names[2]);
    }

    /// <summary>
    /// One arrangement, written straight through the DbContext: both the things
    /// that must appear and the near-misses that must not.
    /// </summary>
    private async Task SeedAsync() => await _factory.WithDbAsync(async db =>
    {
        if (await db.Events.AnyAsync(e => e.Name == "Published market"))
        {
            return true;
        }

        db.SiteSettings.Add(new SiteSetting { Key = "hero_heading", Value = "smol pop-up cafe" });
        db.SiteSettings.Add(new SiteSetting { Key = "not_a_known_key", Value = "should be filtered out" });

        db.Announcements.AddRange(
            new Announcement
            {
                Text = "Running now",
                ActiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
                ActiveUntil = DateTimeOffset.UtcNow.AddDays(7),
                IsActive = true
            },
            new Announcement
            {
                Text = "Finished last month",
                ActiveFrom = DateTimeOffset.UtcNow.AddDays(-40),
                ActiveUntil = DateTimeOffset.UtcNow.AddDays(-30),
                IsActive = true
            });

        db.Events.AddRange(
            NewEvent("Published market", Today.AddDays(3), Today.AddDays(5), EventStatus.Upcoming, published: true),
            NewEvent("Unpublished market", Today.AddDays(4), Today.AddDays(6), EventStatus.Upcoming, published: false),
            NewEvent("Recent past market", Today.AddDays(-10), Today.AddDays(-9), EventStatus.Done, published: true),
            NewEvent("Older past market", Today.AddDays(-40), Today.AddDays(-39), EventStatus.Done, published: true));

        var live = new Package { Name = "Live package", Price = 7500m, GuestCountNote = "50 pax", IsActive = true };
        live.Inclusions.Add(new PackageInclusion { Text = "2 baristas", SortOrder = 0 });
        var retired = new Package { Name = "Retired package", Price = 1m, GuestCountNote = "", IsActive = false };

        db.Packages.AddRange(live, retired);

        var category = new MenuCategory { Name = "Public coffee", SortOrder = 1 };
        var available = new MenuItem { Name = "Available Latte", Description = "Nice", Category = category };
        var soldOut = new MenuItem { Name = "Sold Out Latte", Description = string.Empty, Category = category };
        var draftOnly = new MenuItem { Name = "Draft Only Latte", Description = string.Empty, Category = category };

        var liveVersion = new MenuVersion
        {
            Name = "Live pop-up menu",
            Context = MenuContext.PopUp,
            EffectiveFrom = Today.AddDays(-1),
            IsPublished = true
        };
        liveVersion.Items.Add(new MenuVersionItem { MenuItem = available, Price = 150m, IsAvailable = true });
        liveVersion.Items.Add(new MenuVersionItem { MenuItem = soldOut, Price = 160m, IsAvailable = false, SortOrder = 1 });

        var draftVersion = new MenuVersion
        {
            Name = "Draft pop-up menu",
            Context = MenuContext.PopUp,
            EffectiveFrom = Today,
            IsPublished = false
        };
        draftVersion.Items.Add(new MenuVersionItem { MenuItem = draftOnly, Price = 170m, IsAvailable = true });

        db.MenuVersions.AddRange(liveVersion, draftVersion);

        await db.SaveChangesAsync();
        return true;
    });

    private static Event NewEvent(
        string name,
        DateOnly startsOn,
        DateOnly endsOn,
        EventStatus status,
        bool published) => new()
    {
        Name = name,
        Venue = $"{name} venue",
        StartsOn = startsOn,
        EndsOn = endsOn,
        Status = status,
        IsPublished = published
    };
}
