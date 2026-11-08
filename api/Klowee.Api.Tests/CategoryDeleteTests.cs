using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Menu;

namespace Klowee.Api.Tests;

/// <summary>
/// A category is refused deletion only while a live (non-archived) item uses it.
/// Archived items keep their category_id, and their history still reads with the
/// category's name: the item list, and the versions that priced them.
/// </summary>
public class CategoryDeleteTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public CategoryDeleteTests(KloweeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task OnlyArchivedItems_DeleteSucceeds_AndHistoryKeepsTheCategoryName()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var category = await PostAsync<MenuCategoryDto>(client, "/api/menu/categories", new { name = "Fruit soda (test)", sortOrder = 90 });
        var item = await PostAsync<MenuItemDto>(client, "/api/menu/items", new { name = "Old Fizz", categoryId = category.Id });

        var version = await PostAsync<MenuVersionDetailDto>(client, "/api/menu/versions",
            new { name = "Old fizz menu", context = "Office", effectiveFrom = "2025-06-01" });
        (await client.PutAsJsonAsync($"/api/menu/versions/{version.Id}/items",
            new[] { new { menuItemId = item.Id, price = 99m, isAvailable = true, isFeatured = false, sortOrder = 0 } })).EnsureSuccessStatusCode();
        (await client.PatchAsync($"/api/menu/items/{item.Id}/archive", null)).EnsureSuccessStatusCode();

        var response = await client.DeleteAsync($"/api/menu/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var categories = await client.GetFromJsonAsync<List<MenuCategoryDto>>("/api/menu/categories", KloweeApiFactory.Json);
        Assert.DoesNotContain(categories!, c => c.Id == category.Id);

        var items = await client.GetFromJsonAsync<List<MenuItemDto>>("/api/menu/items?includeArchived=true", KloweeApiFactory.Json);
        var kept = Assert.Single(items!, i => i.Id == item.Id);
        Assert.Equal((category.Id, "Fruit soda (test)", true), (kept.CategoryId, kept.CategoryName, kept.IsArchived));

        var detail = await client.GetFromJsonAsync<MenuVersionDetailDto>($"/api/menu/versions/{version.Id}", KloweeApiFactory.Json);
        Assert.Equal("Fruit soda (test)", Assert.Single(detail!.Items).CategoryName);

        // Unarchiving it would put a live item in a deleted category.
        var unarchive = await client.PatchAsync($"/api/menu/items/{item.Id}/unarchive", null);
        Assert.Equal(HttpStatusCode.Conflict, unarchive.StatusCode);
    }

    [Fact]
    public async Task OneActiveItem_409()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var category = await PostAsync<MenuCategoryDto>(client, "/api/menu/categories", new { name = "Mixed (test)", sortOrder = 91 });
        var archived = await PostAsync<MenuItemDto>(client, "/api/menu/items", new { name = "Mixed Archived", categoryId = category.Id });
        (await client.PatchAsync($"/api/menu/items/{archived.Id}/archive", null)).EnsureSuccessStatusCode();
        await PostAsync<MenuItemDto>(client, "/api/menu/items", new { name = "Mixed Active", categoryId = category.Id });

        var response = await client.DeleteAsync($"/api/menu/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(KloweeApiFactory.Json))!;
    }
}
