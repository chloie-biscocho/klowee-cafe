using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Klowee.Api.Common;
using Klowee.Api.Contracts.Bookings;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Contracts.Packages;
using Klowee.Api.Contracts.Public;
using Klowee.Api.Contracts.Reports;
using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Klowee.Api.Tests;

/// <summary>Booking requests from the homepage (handover 20, decision 023).</summary>
public class BookingTests : IClassFixture<KloweeApiFactory>
{
    /// <summary>"Today" for these tests: deliberately not the machine's date, so nothing can lean on the real clock.</summary>
    private static readonly DateOnly Today = new(2030, 3, 10);

    private readonly KloweeApiFactory _factory;

    public BookingTests(KloweeApiFactory factory)
    {
        _factory = factory;
        _factory.Clock.Today = Today;
    }

    private static object Valid(string name, DateOnly? date = null, Guid? packageId = null, string? website = null, int? guests = 60) => new
    {
        name,
        contact = "0917 555 0101",
        eventDate = (date ?? Today.AddDays(30)).ToString("yyyy-MM-dd"),
        venue = "Limketkai Atrium, Cagayan de Oro",
        guestCount = guests,
        packageId,
        message = "Office anniversary, afternoon.",
        website
    };

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, object body) => client.PostAsJsonAsync("/api/public/bookings", body);

    private async Task<int> CountAsync(string name) =>
        await _factory.WithDbAsync(db => db.BookingRequests.IgnoreQueryFilters().CountAsync(b => b.Name == name));

    private async Task<BookingRequest> StoredAsync(string name) =>
        await _factory.WithDbAsync(db => db.BookingRequests.AsNoTracking().SingleAsync(b => b.Name == name));

    private static async Task<JsonElement> ErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errors");
    }

    private static async Task SetAsync(HttpClient owner, string key, string value)
    {
        var response = await owner.PutAsJsonAsync("/api/settings", new[] { new { key, value } });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<PackageDto> PackageAsync(HttpClient owner, string name, bool active = true)
    {
        var response = await owner.PostAsJsonAsync("/api/packages", new
        {
            name, price = 7500m, description = "", guestCountNote = "good for 50 pax", isActive = active, sortOrder = 1, inclusions = Array.Empty<object>()
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PackageDto>(KloweeApiFactory.Json))!;
    }

    private static async Task<PagedResult<BookingSummaryDto>> ListAsync(HttpClient owner, string query = "") =>
        (await owner.GetFromJsonAsync<PagedResult<BookingSummaryDto>>($"/api/bookings{query}", KloweeApiFactory.Json))!;

    private async Task<Guid> IdOfAsync(string name) => (await StoredAsync(name)).Id;

    // ---- the public form ----

    [Fact]
    public async Task AValidRequest_IsStoredAsNew_AndTheAnswerIsOnlyAnAcknowledgement()
    {
        var anon = await _factory.CreateAnonymousClientAsync();

        var response = await SendAsync(anon, Valid("Stored Visitor"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal("""{"received":true}""", raw);
        Assert.Null(response.Headers.Location);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var stored = await StoredAsync("Stored Visitor");
        Assert.Equal(BookingStatus.New, stored.Status);
        Assert.Equal("0917 555 0101", stored.Contact);
        Assert.Equal(Today.AddDays(30), stored.EventDate);
        Assert.Equal("Limketkai Atrium, Cagayan de Oro", stored.Venue);
        Assert.Equal(60, stored.GuestCount);
        Assert.Equal("Office anniversary, afternoon.", stored.Message);
        Assert.Null(stored.StatusChangedAt);
    }

    [Fact]
    public async Task RequiredFields_AreEachReported_InTheBodysOwnNames()
    {
        var anon = await _factory.CreateAnonymousClientAsync();

        var response = await SendAsync(anon, new { name = " ", contact = "", venue = (string?)null });

        var errors = await ErrorsAsync(response);
        Assert.Equal(["contact", "eventDate", "name", "venue"], errors.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Lengths_GuestsAndMessage_AreChecked_ButContactIsFreeText()
    {
        var anon = await _factory.CreateAnonymousClientAsync();

        var errors = await ErrorsAsync(await SendAsync(anon, new
        {
            name = new string('n', 101), contact = new string('c', 201), eventDate = Today.ToString("yyyy-MM-dd"),
            venue = new string('v', 201), guestCount = 0, message = new string('m', 1001)
        }));
        Assert.Equal(["contact", "guestCount", "message", "name", "venue"], errors.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(anon, Valid("Too many guests", guests: 5001))).StatusCode);

        // Any shape of contact is fine: a handle, a Messenger name, an email.
        foreach (var contact in new[] { "@kee.ig", "Kee Messenger", "kee@example.com", "+63 917 555 0101" })
        {
            var response = await SendAsync(anon, new
            {
                name = $"Contact {contact}", contact, eventDate = Today.ToString("yyyy-MM-dd"), venue = "Here"
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        // Guests and the rest are optional; the edges are allowed.
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, Valid("One guest", guests: 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, Valid("Many guests", guests: 5000))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, Valid("No guests", guests: null))).StatusCode);
    }

    [Fact]
    public async Task TheDate_MustBeTodayOrLater_AndAtMostTwoYearsAhead()
    {
        var anon = await _factory.CreateAnonymousClientAsync();

        var past = await ErrorsAsync(await SendAsync(anon, Valid("Yesterday", Today.AddDays(-1))));
        Assert.Contains("passed", past.GetProperty("eventDate")[0].GetString());
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, Valid("Same day", Today))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, Valid("Two years", Today.AddYears(2)))).StatusCode);
        await ErrorsAsync(await SendAsync(anon, Valid("Too far", Today.AddYears(2).AddDays(1))));
        Assert.Equal(0, await CountAsync("Yesterday"));
        Assert.Equal(0, await CountAsync("Too far"));
    }

    /// <summary>
    /// 2030-03-09 17:30 UTC is already 01:30 on 10 March in Manila (decision
    /// 006). At that moment "today" is the 10th: the 9th has passed, even
    /// though it is still the 9th in UTC.
    /// </summary>
    [Fact]
    public async Task Today_IsTheBusinessTimeZonesDate_NotUtcs()
    {
        await _factory.EnsureInitializedAsync();
        var instant = new DateTimeOffset(2030, 3, 9, 17, 30, 0, TimeSpan.Zero);
        using var manila = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(new SystemClock(Options.Create(new AppOptions { TimeZone = "Asia/Manila" }), new FixedTimeProvider(instant)));
        }));
        var anon = manila.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(anon, Valid("Manila yesterday", new DateOnly(2030, 3, 9)))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, Valid("Manila today", new DateOnly(2030, 3, 10)))).StatusCode);
    }

    [Fact]
    public async Task APackage_MustBeActive_AndItsNameIsCopied_SoARenameDoesNotChangeTheRequest()
    {
        var owner = await _factory.CreateOwnerClientAsync();
        var anon = await _factory.CreateAnonymousClientAsync();
        var starter = await PackageAsync(owner, "Starter booking");
        var hidden = await PackageAsync(owner, "Hidden booking", active: false);

        var unknown = await ErrorsAsync(await SendAsync(anon, Valid("Unknown package", packageId: Guid.NewGuid())));
        Assert.True(unknown.TryGetProperty("packageId", out _));
        await ErrorsAsync(await SendAsync(anon, Valid("Inactive package", packageId: hidden.Id)));
        Assert.Equal(0, await CountAsync("Inactive package"));

        Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, Valid("With package", packageId: starter.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, Valid("Not sure yet"))).StatusCode);

        var rename = await owner.PutAsJsonAsync($"/api/packages/{starter.Id}", new
        {
            name = "Starter renamed", price = 7500m, description = "", guestCountNote = "", isActive = true, sortOrder = 1, inclusions = Array.Empty<object>()
        });
        Assert.True(rename.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/packages/{starter.Id}")).StatusCode);

        var detail = await owner.GetFromJsonAsync<BookingDto>($"/api/bookings/{await IdOfAsync("With package")}", KloweeApiFactory.Json);
        Assert.Equal("Starter booking", detail!.PackageName);
        Assert.Null((await StoredAsync("Not sure yet")).PackageName);
    }

    [Fact]
    public async Task TheHoneypot_GetsTheSameAnswer_AndNothingIsStored()
    {
        var anon = await _factory.CreateAnonymousClientAsync();

        var trapped = await SendAsync(anon, Valid("Honeypot bot", website: "http://spam.example"));
        var person = await SendAsync(anon, Valid("Honeypot person"));

        Assert.Equal(HttpStatusCode.Created, trapped.StatusCode);
        Assert.Equal(await person.Content.ReadAsStringAsync(), await trapped.Content.ReadAsStringAsync());
        Assert.Equal(0, await CountAsync("Honeypot bot"));
        Assert.Equal(1, await CountAsync("Honeypot person"));

        // Even a bot that sends nothing else valid gets the plain 201: it learns nothing.
        var bare = await SendAsync(anon, new { website = "x" });
        Assert.Equal(HttpStatusCode.Created, bare.StatusCode);
    }

    [Fact]
    public async Task WhenTheFormIsSwitchedOff_RequestsAreRefusedWith409()
    {
        var owner = await _factory.CreateOwnerClientAsync();
        var anon = await _factory.CreateAnonymousClientAsync();

        try
        {
            await SetAsync(owner, BookingService.EnabledKey, "FALSE");
            var refused = await SendAsync(anon, Valid("While off"));
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            Assert.Equal("no-store", refused.Headers.CacheControl?.ToString());
            Assert.Equal(0, await CountAsync("While off"));

            // Only "false" switches it off; anything else (empty included) means on.
            await SetAsync(owner, BookingService.EnabledKey, "");
            Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, Valid("While empty"))).StatusCode);
            await SetAsync(owner, BookingService.EnabledKey, "true");
            Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, Valid("While on"))).StatusCode);
        }
        finally
        {
            await SetAsync(owner, BookingService.EnabledKey, "true");
        }
    }

    [Fact]
    public async Task TheSettings_GoOutOnTheHomePayload()
    {
        var owner = await _factory.CreateOwnerClientAsync();
        var anon = await _factory.CreateAnonymousClientAsync();
        await SetAsync(owner, BookingService.EnabledKey, "true");
        await SetAsync(owner, BookingService.NoteKey, "Book at least two weeks ahead.");

        var home = await anon.GetFromJsonAsync<PublicHomeDto>("/api/public/home", KloweeApiFactory.Json);

        Assert.Equal("true", home!.Settings[BookingService.EnabledKey]);
        Assert.Equal("Book at least two weeks ahead.", home.Settings[BookingService.NoteKey]);
    }

    // ---- rate limits ----

    [Fact]
    public async Task AVisitor_GetsThreeAnHour_ThenAFriendly429_AndAnotherVisitorIsUnaffected()
    {
        await _factory.EnsureInitializedAsync();
        using var limited = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<BookingRateLimitOptions>(options =>
            {
                var real = new BookingRateLimitOptions();
                options.PerHour = real.PerHour;
                options.PerDay = real.PerDay;
            })));
        var client = limited.CreateClient();

        // Every attempt counts, a refused one too.
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(client, Valid("Limit one"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, new { name = "Limit two" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(client, Valid("Limit three"))).StatusCode);

        var fourth = await SendAsync(client, Valid("Limit four"));
        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        var problem = await fourth.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(BookingRateLimitOptions.RejectedMessage, problem.GetProperty("detail").GetString());
        Assert.Equal("no-store", fourth.Headers.CacheControl?.ToString());
        Assert.Equal(0, await CountAsync("Limit four"));

        // Another visitor, as Cloudflare reports them, has their own allowance.
        var elsewhere = new HttpRequestMessage(HttpMethod.Post, "/api/public/bookings") { Content = JsonContent.Create(Valid("Limit elsewhere")) };
        elsewhere.Headers.Add("CF-Connecting-IP", "203.0.113.20");
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(elsewhere)).StatusCode);

        // The login limit's 429 is untouched: no booking sentence there.
        Assert.Equal(3, new BookingRateLimitOptions().PerHour);
    }

    [Fact]
    public async Task AVisitor_GetsTenADay_EvenWithTheHourlyLimitOutOfTheWay()
    {
        await _factory.EnsureInitializedAsync();
        using var limited = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<BookingRateLimitOptions>(options =>
            {
                options.PerHour = 1_000;
                options.PerDay = new BookingRateLimitOptions().PerDay;
            })));
        var client = limited.CreateClient();

        for (var i = 1; i <= 10; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await SendAsync(client, Valid($"Daily {i}"))).StatusCode);
        }

        var eleventh = await SendAsync(client, Valid("Daily 11"));
        Assert.Equal(HttpStatusCode.TooManyRequests, eleventh.StatusCode);
        Assert.Equal(BookingRateLimitOptions.RejectedMessage, (await eleventh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal(10, new BookingRateLimitOptions().PerDay);
    }

    /// <summary>
    /// The day's count must survive the hour window resetting. ASP.NET drops a
    /// partition's limiter once it reports itself idle; if the chained limiter
    /// looked idle as soon as its hour refilled, the day count would start over
    /// and the real ceiling would be 3 an hour. Windows shrunk to seconds.
    /// </summary>
    [Fact]
    public async Task TheDayCount_SurvivesTheHourWindowResetting()
    {
        await _factory.EnsureInitializedAsync();
        using var limited = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<BookingRateLimitOptions>(options =>
            {
                options.PerHour = 2;
                options.HourWindow = TimeSpan.FromSeconds(1);
                options.PerDay = 3;
                options.DayWindow = TimeSpan.FromMinutes(2);
            })));
        var client = limited.CreateClient();

        Assert.Equal(HttpStatusCode.Created, (await SendAsync(client, Valid("Rollover 1"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(client, Valid("Rollover 2"))).StatusCode);
        // Long enough for the short window to refill and for the limiter's idle sweep (every 10 s) to run.
        await Task.Delay(TimeSpan.FromSeconds(16));
        Assert.Equal(HttpStatusCode.Created, (await SendAsync(client, Valid("Rollover 3"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SendAsync(client, Valid("Rollover 4"))).StatusCode);
    }

    // ---- the owners ----

    [Theory]
    [InlineData("GET", "/api/bookings")]
    [InlineData("GET", "/api/bookings/{id}")]
    [InlineData("PATCH", "/api/bookings/{id}")]
    [InlineData("DELETE", "/api/bookings/{id}")]
    public async Task Anonymous_CannotReadOrChangeRequests(string method, string path)
    {
        var anon = await _factory.CreateAnonymousClientAsync();
        var name = $"Private visitor {method}{path.Length}";
        await SendAsync(anon, Valid(name));
        var id = await IdOfAsync(name);

        var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{id}", id.ToString()))
        {
            Content = method == "PATCH" ? JsonContent.Create(new { status = "Contacted" }) : null
        };
        var response = await anon.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(BookingStatus.New, (await StoredAsync(name)).Status);
        Assert.Null((await StoredAsync(name)).DeletedAt);
    }

    [Fact]
    public async Task TheList_IsNewestFirst_FiltersByStatus_AndPages()
    {
        var owner = await _factory.CreateOwnerClientAsync();
        var anon = await _factory.CreateAnonymousClientAsync();
        var names = new[] { "List first", "List second", "List third" };
        foreach (var name in names)
        {
            Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, Valid(name))).StatusCode);
            await Task.Delay(15); // distinct created_at
        }

        var contacted = await IdOfAsync("List second");
        await owner.PatchAsJsonAsync($"/api/bookings/{contacted}", new { status = "Contacted" });

        var all = await ListAsync(owner, "?pageSize=200");
        var mine = all.Items.Where(b => names.Contains(b.Name)).Select(b => b.Name).ToList();
        Assert.Equal(["List third", "List second", "List first"], mine);
        Assert.Equal(all.TotalCount, all.Items.Count);

        var onlyNew = await ListAsync(owner, "?status=New&pageSize=200");
        Assert.All(onlyNew.Items, b => Assert.Equal(BookingStatus.New, b.Status));
        Assert.DoesNotContain(onlyNew.Items, b => b.Name == "List second");
        Assert.Contains((await ListAsync(owner, "?status=Contacted")).Items, b => b.Name == "List second");

        var page1 = await ListAsync(owner, "?pageSize=1&page=1");
        var page2 = await ListAsync(owner, "?pageSize=1&page=2");
        Assert.Single(page1.Items);
        Assert.Equal(all.TotalCount, page1.TotalCount);
        Assert.Equal(all.Items[1].Id, page2.Items.Single().Id);
    }

    [Fact]
    public async Task AStatusChange_StampsWhoAndWhen_ButSavingNotesAlone_DoesNot()
    {
        var owner = await _factory.CreateOwnerClientAsync();
        var anon = await _factory.CreateAnonymousClientAsync();
        await SendAsync(anon, Valid("Status visitor"));
        var id = await IdOfAsync("Status visitor");

        var notesOnly = await (await owner.PatchAsJsonAsync($"/api/bookings/{id}", new { status = "New", ownerNotes = "  Called, no answer.  " }))
            .Content.ReadFromJsonAsync<BookingDto>(KloweeApiFactory.Json);
        Assert.Equal("Called, no answer.", notesOnly!.OwnerNotes);
        Assert.Null(notesOnly.StatusChangedAt);
        Assert.Null(notesOnly.StatusChangedByName);

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var contacted = await (await owner.PatchAsJsonAsync($"/api/bookings/{id}", new { status = "Contacted", ownerNotes = "Called, no answer." }))
            .Content.ReadFromJsonAsync<BookingDto>(KloweeApiFactory.Json);
        Assert.Equal(BookingStatus.Contacted, contacted!.Status);
        Assert.Equal(KloweeApiFactory.OwnerDisplayName, contacted.StatusChangedByName);
        Assert.True(contacted.StatusChangedAt >= before);
        var stored = await StoredAsync("Status visitor");
        var ownerId = await _factory.WithDbAsync(db => db.Users.Where(u => u.Email == KloweeApiFactory.OwnerEmail).Select(u => u.Id).SingleAsync());
        Assert.Equal(ownerId, stored.StatusChangedBy);

        // The same status again changes nothing; empty notes clear them.
        var again = await (await owner.PatchAsJsonAsync($"/api/bookings/{id}", new { status = "Contacted", ownerNotes = "" }))
            .Content.ReadFromJsonAsync<BookingDto>(KloweeApiFactory.Json);
        Assert.Equal(contacted.StatusChangedAt, again!.StatusChangedAt);
        Assert.Null(again.OwnerNotes);

        // A status is required; notes have a limit.
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync($"/api/bookings/{id}", new { ownerNotes = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PatchAsJsonAsync($"/api/bookings/{id}", new { status = "Contacted", ownerNotes = new string('n', 1001) })).StatusCode);
    }

    [Fact]
    public async Task Delete_IsSoft_AndTheRequestDisappearsFromTheOwnersView()
    {
        var owner = await _factory.CreateOwnerClientAsync();
        var anon = await _factory.CreateAnonymousClientAsync();
        await SendAsync(anon, Valid("Deleted visitor"));
        var id = await IdOfAsync("Deleted visitor");

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/bookings/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/bookings/{id}")).StatusCode);
        Assert.DoesNotContain((await ListAsync(owner, "?pageSize=200")).Items, b => b.Id == id);
        var row = await _factory.WithDbAsync(db => db.BookingRequests.IgnoreQueryFilters().AsNoTracking().SingleAsync(b => b.Id == id));
        Assert.NotNull(row.DeletedAt);
    }

    [Fact]
    public async Task TheSummary_CountsNewRequests()
    {
        var owner = await _factory.CreateOwnerClientAsync();
        var anon = await _factory.CreateAnonymousClientAsync();
        var before = (await owner.GetFromJsonAsync<SummaryReportDto>("/api/reports/summary", KloweeApiFactory.Json))!.NewBookingsCount;

        await SendAsync(anon, Valid("Summary visitor"));
        Assert.Equal(before + 1, (await owner.GetFromJsonAsync<SummaryReportDto>("/api/reports/summary", KloweeApiFactory.Json))!.NewBookingsCount);

        await owner.PatchAsJsonAsync($"/api/bookings/{await IdOfAsync("Summary visitor")}", new { status = "Declined" });
        Assert.Equal(before, (await owner.GetFromJsonAsync<SummaryReportDto>("/api/reports/summary", KloweeApiFactory.Json))!.NewBookingsCount);
    }

    [Fact]
    public async Task TheVisitorsDetails_NeverReachALog_OnlyTheIdAndTheOutcome()
    {
        var anon = await _factory.CreateAnonymousClientAsync();
        var owner = await _factory.CreateOwnerClientAsync();
        var secret = new
        {
            name = "Logname Zyxwv",
            contact = "logcontact-0917-424242",
            eventDate = Today.AddDays(9).ToString("yyyy-MM-dd"),
            venue = "Logvenue Hall Qwerty",
            guestCount = 40,
            message = "logmessage please keep private"
        };

        Assert.Equal(HttpStatusCode.Created, (await SendAsync(anon, secret)).StatusCode);
        var id = await IdOfAsync("Logname Zyxwv");
        // Invalid, honeypot and owner reads too: none may log the details.
        await SendAsync(anon, secret with { });
        await SendAsync(anon, new { secret.name, secret.contact, secret.venue, secret.message, eventDate = "2001-01-01" });
        await SendAsync(anon, new { secret.name, secret.contact, secret.venue, secret.message, website = "bot" });
        await owner.GetAsync($"/api/bookings/{id}");
        await owner.PatchAsJsonAsync($"/api/bookings/{id}", new { status = "Contacted", ownerNotes = "lognote private" });

        var lines = _factory.Logs.Lines;
        Assert.Contains(lines, l => l.Contains(id.ToString()) && l.Contains("stored"));   // the capture works
        Assert.Contains(lines, l => l.Contains("honeypot"));
        foreach (var value in new[] { "Zyxwv", "logcontact", "424242", "Qwerty", "logmessage", "lognote" })
        {
            Assert.DoesNotContain(lines, l => l.Contains(value, StringComparison.OrdinalIgnoreCase));
        }
    }
}
