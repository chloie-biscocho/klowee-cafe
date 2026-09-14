using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Menu;
using Klowee.Api.Entities;

namespace Klowee.Api.Tests;

/// <summary>The three papercuts handover 03 reported from the admin app.</summary>
public class Part1FixesTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public Part1FixesTests(KloweeApiFactory factory) => _factory = factory;

    /// <summary>
    /// An empty photo URL box used to be a 400, because <c>[Url]</c> saw "" and
    /// rejected it. It now arrives as null, which every optional field accepts.
    /// </summary>
    [Fact]
    public async Task EmptyAndWhitespaceStrings_AreTreatedAsNull()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var category = await CreateCategoryAsync(client, "Blank fields");

        var response = await client.PostAsJsonAsync("/api/menu/items", new
        {
            name = "Item with blank optionals",
            description = "   ",
            categoryId = category.Id,
            photoUrl = ""
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<MenuItemDto>(KloweeApiFactory.Json);
        Assert.NotNull(created);
        Assert.Null(created!.PhotoUrl);
        Assert.Equal(string.Empty, created.Description);
    }

    /// <summary>The same normalisation must not weaken a required field.</summary>
    [Fact]
    public async Task AWhitespaceOnlyRequiredField_IsStillRejected()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/menu/categories",
            new { name = "   ", sortOrder = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task VersionItems_CarryTheirCategoryId()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var category = await CreateCategoryAsync(client, "Category id");
        var item = await CreateItemAsync(client, "Category id Latte", category.Id);
        var version = await CreateVersionAsync(client, "Category id menu");

        var replaced = await client.PutAsJsonAsync(
            $"/api/menu/versions/{version.Id}/items",
            new[] { new MenuVersionItemRequest { MenuItemId = item.Id, Price = 150m, SortOrder = 0 } },
            KloweeApiFactory.Json);

        replaced.EnsureSuccessStatusCode();

        var detail = await replaced.Content.ReadFromJsonAsync<MenuVersionDetailDto>(KloweeApiFactory.Json);
        var line = Assert.Single(detail!.Items);
        Assert.Equal(category.Id, line.CategoryId);
        Assert.Equal(category.Name, line.CategoryName);
    }

    /// <summary>
    /// A PATCH that states a desired state should not mind being sent twice —
    /// a double-click in the admin app is not an error.
    /// </summary>
    [Fact]
    public async Task PublishAndUnpublish_AreIdempotent()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var version = await CreateVersionAsync(client, "Idempotent menu");

        // Unpublishing a draft changes nothing and still succeeds.
        var unpublishDraft = await client.PatchAsync($"/api/menu/versions/{version.Id}/unpublish", null);
        Assert.Equal(HttpStatusCode.OK, unpublishDraft.StatusCode);
        Assert.False((await Read(unpublishDraft))!.IsPublished);

        var first = await client.PatchAsync($"/api/menu/versions/{version.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PatchAsync($"/api/menu/versions/{version.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var after = await Read(second);
        Assert.True(after!.IsPublished);
        Assert.Equal(version.Name, after.Name);
    }

    private static async Task<MenuVersionDetailDto?> Read(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<MenuVersionDetailDto>(KloweeApiFactory.Json);

    private static async Task<MenuCategoryDto> CreateCategoryAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/menu/categories", new { name, sortOrder = 50 });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MenuCategoryDto>(KloweeApiFactory.Json))!;
    }

    private static async Task<MenuItemDto> CreateItemAsync(HttpClient client, string name, Guid categoryId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/menu/items",
            new { name, description = (string?)null, categoryId, photoUrl = (string?)null });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MenuItemDto>(KloweeApiFactory.Json))!;
    }

    private static async Task<MenuVersionDetailDto> CreateVersionAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/menu/versions", new CreateMenuVersionRequest
        {
            Name = name,
            Context = MenuContext.PopUp,
            EffectiveFrom = new DateOnly(2026, 6, 1)
        }, KloweeApiFactory.Json);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MenuVersionDetailDto>(KloweeApiFactory.Json))!;
    }
}
