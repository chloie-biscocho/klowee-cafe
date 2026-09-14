using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Packages;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Tests;

public class PackageTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public PackageTests(KloweeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_StoresTheInclusionsWithThePackage()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var created = await CreateAsync(client, "Create package", ["Good for 50 pax", "2 baristas"]);

        Assert.Equal(2, created.Inclusions.Count);
        Assert.Equal(["Good for 50 pax", "2 baristas"], created.Inclusions.Select(i => i.Text));
        Assert.True(created.IsActive);
    }

    /// <summary>
    /// A PUT replaces the inclusion list wholesale — the same contract as menu
    /// version items. Sending two lines where there were three removes one.
    /// </summary>
    [Fact]
    public async Task Update_ReplacesTheWholeInclusionList()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var created = await CreateAsync(client, "Replace package", ["One", "Two", "Three"]);

        var response = await client.PutAsJsonAsync($"/api/packages/{created.Id}", new
        {
            name = "Replace package",
            price = 9000m,
            description = "Updated",
            guestCountNote = "good for 60 pax",
            isActive = true,
            sortOrder = 2,
            inclusions = new[]
            {
                new { text = "Only this one", sortOrder = 0 },
                new { text = "And this one", sortOrder = 1 }
            }
        });

        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<PackageDto>(KloweeApiFactory.Json);
        Assert.Equal(["Only this one", "And this one"], updated!.Inclusions.Select(i => i.Text));
        Assert.Equal(9000m, updated.Price);

        // The removed lines are gone from a fresh read, not just from the response.
        var reread = await client.GetFromJsonAsync<PackageDto>(
            $"/api/packages/{created.Id}", KloweeApiFactory.Json);
        Assert.Equal(2, reread!.Inclusions.Count);
        Assert.DoesNotContain(reread.Inclusions, i => i.Text == "Three");
    }

    [Fact]
    public async Task Deactivate_HidesThePackageFromTheDefaultList()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var created = await CreateAsync(client, "Hidden package", ["Something"]);

        var deactivated = await client.PatchAsync($"/api/packages/{created.Id}/deactivate", null);
        deactivated.EnsureSuccessStatusCode();

        var active = await client.GetFromJsonAsync<List<PackageDto>>("/api/packages", KloweeApiFactory.Json);
        Assert.DoesNotContain(active!, p => p.Id == created.Id);

        var all = await client.GetFromJsonAsync<List<PackageDto>>(
            "/api/packages?includeInactive=true", KloweeApiFactory.Json);
        Assert.Contains(all!, p => p.Id == created.Id && !p.IsActive);
    }

    [Fact]
    public async Task Delete_AlsoRemovesTheInclusions()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var created = await CreateAsync(client, "Doomed package", ["Gone too"]);

        var deleted = await client.DeleteAsync($"/api/packages/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var gone = await client.GetAsync($"/api/packages/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        var orphans = await _factory.WithDbAsync(async db =>
            await db.PackageInclusions.CountAsync(i => i.PackageId == created.Id));
        Assert.Equal(0, orphans);
    }

    [Fact]
    public async Task Create_WithADuplicateName_Returns409()
    {
        var client = await _factory.CreateOwnerClientAsync();
        await CreateAsync(client, "Only one of me", ["x"]);

        var response = await client.PostAsJsonAsync("/api/packages", new
        {
            name = "Only one of me",
            price = 1m,
            description = (string?)null,
            guestCountNote = (string?)null,
            isActive = true,
            sortOrder = 9,
            inclusions = Array.Empty<object>()
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static async Task<PackageDto> CreateAsync(HttpClient client, string name, string[] inclusions)
    {
        var response = await client.PostAsJsonAsync("/api/packages", new
        {
            name,
            price = 7500m,
            description = "A package",
            guestCountNote = "good for 50 pax",
            isActive = true,
            sortOrder = 1,
            inclusions = inclusions.Select((text, index) => new { text, sortOrder = index }).ToArray()
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PackageDto>(KloweeApiFactory.Json))!;
    }
}
