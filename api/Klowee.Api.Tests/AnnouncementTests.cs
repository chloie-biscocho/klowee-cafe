using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Announcements;
using Klowee.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Klowee.Api.Tests;

public class AnnouncementTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public AnnouncementTests(KloweeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_WithActiveUntilBeforeActiveFrom_Returns400()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PostAsJsonAsync("/api/announcements", new
        {
            text = "Backwards window",
            activeFrom = "2026-06-10T00:00:00+08:00",
            activeUntil = "2026-06-09T00:00:00+08:00",
            isActive = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("must be after activeFrom", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_WithNoActiveUntil_IsOpenEnded()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var created = await CreateAsync(client, "Open ended", DateTimeOffset.UtcNow.AddDays(-1), until: null);

        Assert.Null(created.ActiveUntil);
    }

    /// <summary>
    /// The window rule: active, started, not yet expired. Everything outside that
    /// is invisible to the public site, which is what the exclusion test on
    /// /api/public/home relies on.
    /// </summary>
    [Fact]
    public async Task Deactivating_TakesAnAnnouncementOutOfTheWindow()
    {
        var client = await _factory.CreateOwnerClientAsync();
        await ClearAnnouncementsAsync();
        var now = DateTimeOffset.UtcNow;

        var live = await CreateAsync(client, "Currently running", now.AddHours(-1), now.AddDays(7));

        var beforeDeactivate = await CurrentTextAsync();
        Assert.Equal("Currently running", beforeDeactivate);

        (await client.PatchAsync($"/api/announcements/{live.Id}/deactivate", null))
            .EnsureSuccessStatusCode();

        Assert.Null(await CurrentTextAsync());

        (await client.PatchAsync($"/api/announcements/{live.Id}/activate", null))
            .EnsureSuccessStatusCode();

        Assert.Equal("Currently running", await CurrentTextAsync());
    }

    [Fact]
    public async Task FutureAndExpiredAnnouncements_AreNotCurrent()
    {
        var client = await _factory.CreateOwnerClientAsync();
        await ClearAnnouncementsAsync();
        var now = DateTimeOffset.UtcNow;

        await CreateAsync(client, "Not yet", now.AddDays(30), now.AddDays(31));
        await CreateAsync(client, "Long over", now.AddDays(-30), now.AddDays(-29));

        Assert.Null(await CurrentTextAsync());
    }

    [Fact]
    public async Task WhenSeveralAreRunning_TheNewestWindowWins()
    {
        var client = await _factory.CreateOwnerClientAsync();
        await ClearAnnouncementsAsync();
        var now = DateTimeOffset.UtcNow;

        await CreateAsync(client, "Older banner", now.AddDays(-5), now.AddDays(5));
        await CreateAsync(client, "Newer banner", now.AddHours(-2), now.AddDays(5));

        Assert.Equal("Newer banner", await CurrentTextAsync());
    }

    /// <summary>
    /// Windows are stored as UTC instants. Npgsql rejects a DateTimeOffset with
    /// a non-zero offset on a timestamptz column, and the admin app will always
    /// send +08:00 — this is the regression test for that.
    /// </summary>
    [Fact]
    public async Task AWindowSentInManilaTime_IsStoredAsUtc()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PostAsJsonAsync("/api/announcements", new
        {
            text = "Manila time window",
            activeFrom = "2026-09-13T08:00:00+08:00",
            activeUntil = "2026-09-30T08:00:00+08:00",
            isActive = true
        });

        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<AnnouncementDto>(KloweeApiFactory.Json);
        Assert.Equal(TimeSpan.Zero, created!.ActiveFrom.Offset);
        Assert.Equal(TimeSpan.Zero, created.ActiveUntil!.Value.Offset);

        // Same instant, expressed differently: 08:00 in Manila is midnight UTC.
        Assert.Equal(new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero), created.ActiveFrom);
    }

    /// <summary>
    /// Hard-deletes every announcement, soft-deleted ones included. The class
    /// fixture shares one database across its tests, and "what is current" is a
    /// question about the whole table.
    /// </summary>
    private Task ClearAnnouncementsAsync() =>
        _factory.WithDbAsync(async db =>
            await db.Announcements.IgnoreQueryFilters().ExecuteDeleteAsync());

    /// <summary>Reads the rule through the service, since it has no admin endpoint of its own.</summary>
    private async Task<string?> CurrentTextAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var announcements = scope.ServiceProvider.GetRequiredService<IAnnouncementService>();
        var current = await announcements.GetCurrentAsync(CancellationToken.None);
        return current?.Text;
    }

    private static async Task<AnnouncementDto> CreateAsync(
        HttpClient client,
        string text,
        DateTimeOffset from,
        DateTimeOffset? until)
    {
        var response = await client.PostAsJsonAsync("/api/announcements", new AnnouncementRequest
        {
            Text = text,
            ActiveFrom = from,
            ActiveUntil = until,
            IsActive = true
        }, KloweeApiFactory.Json);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AnnouncementDto>(KloweeApiFactory.Json))!;
    }
}
