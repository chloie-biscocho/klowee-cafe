using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Events;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Tests;

public class EventTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public EventTests(KloweeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_WithEndsOnBeforeStartsOn_Returns400()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var response = await client.PostAsJsonAsync("/api/events", new
        {
            name = "Backwards market",
            venue = "Somewhere",
            startsOn = "2026-06-10",
            endsOn = "2026-06-09",
            status = "Upcoming"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("before startsOn", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Create_WithASingleDay_IsAccepted()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var created = await CreateAsync(client, "One day pop-up", EventStatus.Upcoming,
            new DateOnly(2026, 6, 10), new DateOnly(2026, 6, 10));

        Assert.Equal(created.StartsOn, created.EndsOn);
        Assert.False(created.IsPublished);
    }

    [Theory]
    [InlineData(EventStatus.Planned)]
    [InlineData(EventStatus.Cancelled)]
    public async Task Publish_WithAStatusThatCannotGoLive_Returns409(EventStatus status)
    {
        var client = await _factory.CreateOwnerClientAsync();
        var created = await CreateAsync(client, $"Unpublishable {status}", status,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2));

        var response = await client.PatchAsync($"/api/events/{created.Id}/publish", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(
            "Planned and Cancelled events can't be published",
            await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(EventStatus.Upcoming)]
    [InlineData(EventStatus.Done)]
    public async Task Publish_WithAStatusThatCanGoLive_Succeeds(EventStatus status)
    {
        var client = await _factory.CreateOwnerClientAsync();
        var created = await CreateAsync(client, $"Publishable {status}", status,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 2));

        var response = await client.PatchAsync($"/api/events/{created.Id}/publish", null);
        response.EnsureSuccessStatusCode();

        var published = await response.Content.ReadFromJsonAsync<EventDetailDto>(KloweeApiFactory.Json);
        Assert.True(published!.IsPublished);
    }

    [Fact]
    public async Task ReplacePhotos_SwapsTheWholeList()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var created = await CreateAsync(client, "Photo event", EventStatus.Done,
            new DateOnly(2026, 5, 21), new DateOnly(2026, 5, 24));

        await client.PutAsJsonAsync($"/api/events/{created.Id}/photos", new[]
        {
            new { url = "https://cdn.test/a.jpg", caption = "Cart", sortOrder = 0 },
            new { url = "https://cdn.test/b.jpg", caption = (string?)null, sortOrder = 1 },
            new { url = "https://cdn.test/c.jpg", caption = "Queue", sortOrder = 2 }
        });

        var response = await client.PutAsJsonAsync($"/api/events/{created.Id}/photos", new[]
        {
            new { url = "https://cdn.test/only.jpg", caption = "The only one", sortOrder = 0 }
        });

        response.EnsureSuccessStatusCode();

        var detail = await response.Content.ReadFromJsonAsync<EventDetailDto>(KloweeApiFactory.Json);
        var photo = Assert.Single(detail!.Photos);
        Assert.Equal("https://cdn.test/only.jpg", photo.Url);

        var live = await _factory.WithDbAsync(async db =>
            await db.EventPhotos.CountAsync(p => p.EventId == created.Id));
        Assert.Equal(1, live);
    }

    /// <summary>
    /// "Next" is measured against ends_on in Manila, so a market that is midway
    /// through is still the next event, and a finished one is not.
    /// </summary>
    [Fact]
    public async Task List_FiltersByStatusAndPublishedState()
    {
        var client = await _factory.CreateOwnerClientAsync();

        var done = await CreateAsync(client, "Filter done", EventStatus.Done,
            new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 11));
        await CreateAsync(client, "Filter planned", EventStatus.Planned,
            new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 13));

        (await client.PatchAsync($"/api/events/{done.Id}/publish", null)).EnsureSuccessStatusCode();

        var published = await client.GetFromJsonAsync<List<EventSummaryDto>>(
            "/api/events?published=true", KloweeApiFactory.Json);
        Assert.Contains(published!, e => e.Id == done.Id);
        Assert.DoesNotContain(published!, e => e.Name == "Filter planned");

        var planned = await client.GetFromJsonAsync<List<EventSummaryDto>>(
            "/api/events?status=Planned", KloweeApiFactory.Json);
        Assert.All(planned!, e => Assert.Equal(EventStatus.Planned, e.Status));
    }

    [Fact]
    public async Task Delete_AlsoRemovesThePhotos()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var created = await CreateAsync(client, "Doomed event", EventStatus.Done,
            new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1));

        await client.PutAsJsonAsync($"/api/events/{created.Id}/photos", new[]
        {
            new { url = "https://cdn.test/gone.jpg", caption = (string?)null, sortOrder = 0 }
        });

        var deleted = await client.DeleteAsync($"/api/events/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var orphans = await _factory.WithDbAsync(async db =>
            await db.EventPhotos.CountAsync(p => p.EventId == created.Id));
        Assert.Equal(0, orphans);
    }

    private static async Task<EventDetailDto> CreateAsync(
        HttpClient client,
        string name,
        EventStatus status,
        DateOnly startsOn,
        DateOnly endsOn)
    {
        var response = await client.PostAsJsonAsync("/api/events", new EventRequest
        {
            Name = name,
            Venue = "Test venue",
            StartsOn = startsOn,
            EndsOn = endsOn,
            Status = status
        }, KloweeApiFactory.Json);

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventDetailDto>(KloweeApiFactory.Json))!;
    }
}
