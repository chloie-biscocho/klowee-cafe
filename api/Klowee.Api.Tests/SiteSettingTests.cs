using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Settings;

namespace Klowee.Api.Tests;

public class SiteSettingTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public SiteSettingTests(KloweeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Upsert_WithAnUnknownKey_Returns400AndChangesNothing()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PutAsJsonAsync("/api/settings", new[]
        {
            new { key = "hero_heading", value = "Should not be saved" },
            new { key = "hero_headline", value = "typo in the key" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("hero_headline", body);
        Assert.Contains("Known keys are", body);

        // The whole payload is rejected, so the valid half is not written either.
        var settings = await ListAsync(client);
        Assert.DoesNotContain(settings, s => s.Value == "Should not be saved");
    }

    [Fact]
    public async Task Upsert_CreatesMissingKeysAndUpdatesExistingOnes()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var first = await client.PutAsJsonAsync("/api/settings", new[]
        {
            new { key = "hero_heading", value = "smol pop-up cafe" },
            new { key = "hero_image_url", value = "https://cdn.test/hero.png" }
        });

        first.EnsureSuccessStatusCode();

        var afterFirst = await ListAsync(client);
        Assert.Equal("smol pop-up cafe", Value(afterFirst, "hero_heading"));
        Assert.Equal("https://cdn.test/hero.png", Value(afterFirst, "hero_image_url"));

        var second = await client.PutAsJsonAsync("/api/settings", new[]
        {
            new { key = "hero_heading", value = "big on matcha" }
        });

        second.EnsureSuccessStatusCode();

        var afterSecond = await ListAsync(client);
        Assert.Equal("big on matcha", Value(afterSecond, "hero_heading"));
        // Keys left out of the payload are untouched, not cleared.
        Assert.Equal("https://cdn.test/hero.png", Value(afterSecond, "hero_image_url"));
    }

    /// <summary>An empty box clears a setting: the converter makes it null, and null means "".</summary>
    [Fact]
    public async Task Upsert_WithAnEmptyValue_ClearsTheSetting()
    {
        var client = await _factory.CreateOwnerClientAsync();

        await client.PutAsJsonAsync("/api/settings", new[]
        {
            new { key = "ticker_fallback", value = "Something" }
        });

        var response = await client.PutAsJsonAsync("/api/settings", new[]
        {
            new { key = "ticker_fallback", value = "" }
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal(string.Empty, Value(await ListAsync(client), "ticker_fallback"));
    }

    [Fact]
    public async Task Upsert_WithTheSameKeyTwice_Returns400()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PutAsJsonAsync("/api/settings", new[]
        {
            new { key = "story_body", value = "one" },
            new { key = "story_body", value = "two" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string? Value(List<SiteSettingDto> settings, string key) =>
        settings.FirstOrDefault(s => s.Key == key)?.Value;

    private static async Task<List<SiteSettingDto>> ListAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<List<SiteSettingDto>>("/api/settings", KloweeApiFactory.Json))!;
}
