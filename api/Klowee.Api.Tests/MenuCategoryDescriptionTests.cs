using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Menu;

namespace Klowee.Api.Tests;

/// <summary>A category's optional description: saved, returned, trimmed, cleared by a blank, capped at 200.</summary>
public class MenuCategoryDescriptionTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public MenuCategoryDescriptionTests(KloweeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Description_RoundTrips_AndABlankClearsIt()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var created = await client.PostAsJsonAsync("/api/menu/categories",
            new { name = "Houseblend (desc test)", sortOrder = 5, description = "  oat milk + ceremonial matcha  " });
        created.EnsureSuccessStatusCode();
        var category = (await created.Content.ReadFromJsonAsync<MenuCategoryDto>(KloweeApiFactory.Json))!;
        Assert.Equal("oat milk + ceremonial matcha", category.Description);

        var listed = await client.GetFromJsonAsync<List<MenuCategoryDto>>("/api/menu/categories", KloweeApiFactory.Json);
        Assert.Equal("oat milk + ceremonial matcha", Assert.Single(listed!, c => c.Id == category.Id).Description);

        var updated = await client.PutAsJsonAsync($"/api/menu/categories/{category.Id}",
            new { name = "Houseblend (desc test)", sortOrder = 5, description = "   " });
        updated.EnsureSuccessStatusCode();
        Assert.Null((await updated.Content.ReadFromJsonAsync<MenuCategoryDto>(KloweeApiFactory.Json))!.Description);
    }

    [Fact]
    public async Task Description_Over200Characters_Is400()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PostAsJsonAsync("/api/menu/categories",
            new { name = "Too long (desc test)", sortOrder = 6, description = new string('x', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
