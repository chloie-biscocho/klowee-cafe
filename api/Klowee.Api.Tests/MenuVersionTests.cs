using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Menu;
using Klowee.Api.Entities;

namespace Klowee.Api.Tests;

public class MenuVersionTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public MenuVersionTests(KloweeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_WithCopyFromVersionId_CopiesTheItems()
    {
        var source = await SeedDraftVersionWithItemsAsync("Copy", MenuContext.PopUp);
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PostAsJsonAsync("/api/menu/versions", new CreateMenuVersionRequest
        {
            Name = "Copied pop-up menu",
            Context = MenuContext.PopUp,
            EffectiveFrom = new DateOnly(2026, 5, 1),
            CopyFromVersionId = source.VersionId
        }, KloweeApiFactory.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<MenuVersionDetailDto>(KloweeApiFactory.Json);
        Assert.NotNull(created);
        Assert.NotEqual(source.VersionId, created!.Id);
        Assert.False(created.IsPublished);

        Assert.Equal(
            source.ItemIds.OrderBy(id => id),
            created.Items.Select(i => i.MenuItemId).OrderBy(id => id));

        // Prices and flags come across too, not just the item references.
        Assert.Equal([99m, 120m], created.Items.Select(i => i.Price).OrderBy(p => p));
        Assert.Contains(created.Items, i => i.IsFeatured);
    }

    [Fact]
    public async Task ReplaceItems_OnPublishedVersion_Returns409()
    {
        var seeded = await SeedDraftVersionWithItemsAsync("Locked", MenuContext.PopUp);
        var client = await _factory.CreateOwnerClientAsync();

        var publish = await client.PatchAsync($"/api/menu/versions/{seeded.VersionId}/publish", null);
        publish.EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync(
            $"/api/menu/versions/{seeded.VersionId}/items",
            new[] { new MenuVersionItemRequest { MenuItemId = seeded.ItemIds[0], Price = 175m, SortOrder = 0 } },
            KloweeApiFactory.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ReplaceItems_OnDraftVersion_ReplacesTheWholeList()
    {
        var seeded = await SeedDraftVersionWithItemsAsync("Redraw", MenuContext.PopUp);
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PutAsJsonAsync(
            $"/api/menu/versions/{seeded.VersionId}/items",
            new[] { new MenuVersionItemRequest { MenuItemId = seeded.ItemIds[1], Price = 175m, SortOrder = 0 } },
            KloweeApiFactory.Json);

        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<MenuVersionDetailDto>(KloweeApiFactory.Json);
        var item = Assert.Single(updated!.Items);
        Assert.Equal(seeded.ItemIds[1], item.MenuItemId);
        Assert.Equal(175m, item.Price);
    }

    [Fact]
    public async Task GetCurrent_ReturnsNewestPublished_IgnoringDraftsAndFutureVersions()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await _factory.WithDbAsync(async db =>
        {
            var category = new MenuCategory { Name = "Office drinks", SortOrder = 1 };
            var item = new MenuItem { Name = "Office Americano", Description = string.Empty, Category = category };

            var live = NewVersion("Live office menu", today.AddDays(-1), isPublished: true);
            live.Items.Add(new MenuVersionItem { MenuItem = item, Price = 105m, SortOrder = 0 });

            db.MenuVersions.AddRange(
                NewVersion("Older office menu", today.AddDays(-30), isPublished: true),
                live,
                NewVersion("Future office menu", today.AddDays(30), isPublished: true),
                NewVersion("Draft office menu", today, isPublished: false));

            await db.SaveChangesAsync();
            return true;
        });

        var client = await _factory.CreateAnonymousClientAsync();

        var current = await client.GetFromJsonAsync<MenuVersionDetailDto>(
            "/api/menu/versions/current?context=Office", KloweeApiFactory.Json);

        Assert.NotNull(current);
        Assert.Equal("Live office menu", current!.Name);
        Assert.True(current.IsPublished);
        Assert.Equal(MenuContext.Office, current.Context);

        var item = Assert.Single(current.Items);
        Assert.Equal("Office Americano", item.Name);
        Assert.Equal(105m, item.Price);
    }

    private static MenuVersion NewVersion(string name, DateOnly effectiveFrom, bool isPublished) => new()
    {
        Name = name,
        Context = MenuContext.Office,
        EffectiveFrom = effectiveFrom,
        IsPublished = isPublished
    };

    /// <summary>
    /// Arranges a draft version with two priced items straight through the
    /// DbContext. Names are prefixed per test because menu item and category
    /// names are uniquely indexed.
    /// </summary>
    private async Task<(Guid VersionId, Guid[] ItemIds)> SeedDraftVersionWithItemsAsync(
        string prefix,
        MenuContext context) =>
        await _factory.WithDbAsync(async db =>
        {
            var category = new MenuCategory { Name = $"{prefix} drinks", SortOrder = 1 };

            var latte = new MenuItem { Name = $"{prefix} Latte", Description = string.Empty, Category = category };
            var soda = new MenuItem { Name = $"{prefix} Soda", Description = string.Empty, Category = category };

            var version = new MenuVersion
            {
                Name = $"{prefix} source menu",
                Context = context,
                EffectiveFrom = new DateOnly(2026, 1, 1),
                IsPublished = false
            };

            version.Items.Add(new MenuVersionItem { MenuItem = latte, Price = 120m, SortOrder = 0 });
            version.Items.Add(new MenuVersionItem { MenuItem = soda, Price = 99m, SortOrder = 1, IsFeatured = true });

            db.MenuVersions.Add(version);
            await db.SaveChangesAsync();

            return (version.Id, new[] { latte.Id, soda.Id });
        });
}
