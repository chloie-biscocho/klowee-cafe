using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Klowee.Api.Common;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Contracts.Reports;
using Klowee.Api.Contracts.Tabs;
using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Klowee.Api.Tests.SalesFixture;

namespace Klowee.Api.Tests;

/// <summary>Customers' private tab pages (handover 17, decision 021).</summary>
public class TabTests : IClassFixture<KloweeApiFactory>
{
    private const string Suffix = "tabs";
    private readonly KloweeApiFactory _factory;

    public TabTests(KloweeApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Owner, HttpClient Public, SalesMenu Menu)> SetupAsync() =>
        (await _factory.CreateOwnerClientAsync(), await _factory.CreateAnonymousClientAsync(), await SeedMenuAsync(_factory, Suffix));

    private static async Task<string> LinkAsync(HttpClient owner, Guid customerId)
    {
        var response = await owner.PostAsync($"/api/customers/{customerId}/tab-link", null);
        await EnsureAsync(response);
        var link = (await response.Content.ReadFromJsonAsync<TabLinkDto>(KloweeApiFactory.Json))!;
        Assert.StartsWith(KloweeApiFactory.PublicSiteUrl + "/tab/", link.Url);
        return link.Url[(KloweeApiFactory.PublicSiteUrl.Length + "/tab/".Length)..];
    }

    private static async Task<PublicTabDto> TabAsync(HttpClient client, string token)
    {
        var response = await client.GetAsync($"/api/public/tab/{token}");
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<PublicTabDto>(KloweeApiFactory.Json))!;
    }

    private static Task<HttpResponseMessage> ClaimAsync(HttpClient client, string token, decimal amount, string reference, Guid methodId) =>
        client.PostAsJsonAsync($"/api/public/tab/{token}/claims", new { amount, reference, paymentMethodId = methodId });

    private static async Task<PaymentMethodDto> MethodAsync(HttpClient owner, string label, PaymentChannel channel = PaymentChannel.GCash, bool active = true)
    {
        var response = await owner.PostAsJsonAsync("/api/payment-methods",
            new { channel = channel.ToString(), label, accountName = "Klowee Cafe", accountNumber = "0917 000 0000", isActive = active, sortOrder = 1 });
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<PaymentMethodDto>(KloweeApiFactory.Json))!;
    }

    private static async Task<PaymentClaimDto> OwnerClaimAsync(HttpClient owner, Guid id) =>
        (await owner.GetFromJsonAsync<List<PaymentClaimDto>>("/api/payment-claims", KloweeApiFactory.Json))!.Single(c => c.Id == id);

    private static async Task<SummaryReportDto> SummaryAsync(HttpClient owner) =>
        (await owner.GetFromJsonAsync<SummaryReportDto>("/api/reports/summary", KloweeApiFactory.Json))!;

    // ---- links ----

    [Fact]
    public async Task ALink_IsA43CharacterRandomToken_AndResettingItKillsTheOldOne()
    {
        var (owner, anon, _) = await SetupAsync();
        var kee = await CreateCustomerAsync(owner, "Kee link");
        Assert.False((await GetCustomerAsync(owner, kee.Id)).HasTabLink);

        var first = await LinkAsync(owner, kee.Id);
        Assert.Equal(43, first.Length);   // 32 bytes, base64url, no padding
        Assert.Matches("^[A-Za-z0-9_-]{43}$", first);
        var detail = await GetCustomerAsync(owner, kee.Id);
        Assert.True(detail.HasTabLink);
        Assert.Equal($"{KloweeApiFactory.PublicSiteUrl}/tab/{first}", detail.TabUrl);
        Assert.NotNull(detail.TabLinkCreatedAt);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/public/tab/{first}")).StatusCode);

        // Reset: a new token, and the old one is dead at once.
        var second = await LinkAsync(owner, kee.Id);
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/tab/{first}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/public/tab/{second}")).StatusCode);

        // Remove: no link at all.
        await EnsureAsync(await owner.DeleteAsync($"/api/customers/{kee.Id}/tab-link"));
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/tab/{second}")).StatusCode);
        detail = await GetCustomerAsync(owner, kee.Id);
        Assert.False(detail.HasTabLink);
        Assert.Null(detail.TabUrl);

        // The list carries the flag too.
        await LinkAsync(owner, kee.Id);
        var list = await owner.GetStringAsync("/api/customers?search=Kee%20link");
        Assert.Contains("\"hasTabLink\":true", list);
    }

    [Fact]
    public async Task AnyTokenThatIsNotALiveLink_GetsTheSame404()
    {
        var (owner, anon, _) = await SetupAsync();
        var method = await MethodAsync(owner, "GCash unknown");

        var made = Base64UrlLike();
        var responses = new[]
        {
            await anon.GetAsync("/api/public/tab/nope"),
            await anon.GetAsync($"/api/public/tab/{made}"),
            await ClaimAsync(anon, made, 10m, "ABC123456", method.Id),
            await anon.PostAsync($"/api/public/tab/{made}/clubs/{LoyaltyClub.CoffeeClubId}/request", null)
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("This link isn't active.", body.RootElement.GetProperty("detail").GetString());
        }

        // Anonymous callers reach nothing else.
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/payment-claims")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/payment-methods")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/loyalty/card-requests")).StatusCode);
    }

    private static string Base64UrlLike() => TabLinks.NewToken();

    // ---- the payload ----

    [Fact]
    public async Task ThePayload_IsThePayersTab_WithCareOfOrders_AndTheDrinkersOwnLoyalty_AndNothingElse()
    {
        var (owner, anon, m) = await SetupAsync();
        var prince = await CreateCustomerAsync(owner, "Prince Payload Fullname", "Prince P");
        var kee = await CreateCardholderAsync(_factory, owner, "Kee Payload Secretname");

        // Kee's latte, care of Prince: on Prince's tab, stamping Kee's card.
        var careOf = await CreateOrderAsync(owner, OrderBody(kee.Id, [Line(m.Latte)], payerId: prince.Id,
            orderedOn: new DateOnly(2026, 11, 1), notes: "private note for the owners"));
        // Prince's own americano, part paid.
        var own = await CreateOrderAsync(owner, OrderBody(prince.Id, [Line(m.Americano)], orderedOn: new DateOnly(2026, 11, 3)));
        await EnsureAsync(await PayAsync(owner, own.Id, 10m));
        // Kee's own matcha, paid: stamps Kee, not on Prince's tab.
        var keeOwn = await CreateOrderAsync(owner, OrderBody(kee.Id, [Line(m.Matcha)], orderedOn: new DateOnly(2026, 11, 2)));
        await EnsureAsync(await PayAsync(owner, keeOwn.Id, keeOwn.Total));

        var gcash = await MethodAsync(owner, "GCash payload");
        var hidden = await MethodAsync(owner, "Maya payload", PaymentChannel.Maya, active: false);
        await EnsureAsync(await owner.PutAsJsonAsync("/api/settings", new[] { new { key = PublicTabService.NoteKey, value = "Send the exact amount, please." } }));

        var princeToken = await LinkAsync(owner, prince.Id);
        var raw = await anon.GetStringAsync($"/api/public/tab/{princeToken}");
        var tab = JsonSerializer.Deserialize<PublicTabDto>(raw, KloweeApiFactory.Json)!;

        Assert.Equal("Prince P", tab.Name);
        Assert.Equal(260m, tab.Balance);
        Assert.Equal(0m, tab.PendingClaimsTotal);
        Assert.Equal(
            [new PublicOrderDto(new DateOnly(2026, 11, 1), $"Latte {Suffix}", 160m, 0m, 160m),
             new PublicOrderDto(new DateOnly(2026, 11, 3), $"Americano {Suffix}", 110m, 10m, 100m)],
            tab.UnpaidOrders);
        Assert.Equal([new PublicPaymentDto(Today, PaymentChannel.GCash, 10m)], tab.RecentPayments);
        Assert.Empty(tab.Claims);
        Assert.Equal("Send the exact amount, please.", tab.Pay.Note);
        Assert.Contains(tab.Pay.Methods, x => x.Id == gcash.Id && x.Label == "GCash payload" && x.AccountName == "Klowee Cafe");
        Assert.DoesNotContain(tab.Pay.Methods, x => x.Id == hidden.Id);

        // Prince holds no card: the club shows, with nothing on it.
        var princeCoffee = Assert.Single(tab.Clubs, c => c.ClubId == LoyaltyClub.CoffeeClubId);
        Assert.Equal(("none", 0, 0), (princeCoffee.Membership, princeCoffee.Position, princeCoffee.Cards.Count));
        // ...but its printed rewards, all locked, for the dimmed card.
        Assert.Equal([5, 8, 12], princeCoffee.Rewards.Select(r => r.Slot));
        Assert.All(princeCoffee.Rewards, r => Assert.Equal(("locked", 0), (r.State, r.Cycle)));

        // No one else's name, no internal ids, no notes.
        foreach (var secret in new[] { "Secretname", "Fullname", kee.Id.ToString(), prince.Id.ToString(), careOf.Id.ToString(), own.Id.ToString(), "private note" })
        {
            Assert.DoesNotContain(secret, raw, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var key in new[] { "\"customerId\"", "\"orderId\"", "\"membershipId\"", "\"notes\"", "\"note\":\"private" })
        {
            Assert.DoesNotContain(key, raw);
        }

        // Kee's page: nothing owed (the latte is Prince's to pay), the matcha stamped.
        var keeTab = await TabAsync(anon, await LinkAsync(owner, kee.Id));
        Assert.Equal(0m, keeTab.Balance);
        Assert.Empty(keeTab.UnpaidOrders);
        var coffee = Assert.Single(keeTab.Clubs, c => c.ClubId == LoyaltyClub.CoffeeClubId);
        Assert.Equal(("member", 1, 12), (coffee.Membership, coffee.Position, coffee.SlotCount));
        Assert.Equal('u', coffee.Name[coffee.AccentLetter!.Value]);   // the paper card's pink U (handover 17a)
        Assert.Equal(1, coffee.PendingDrinks);   // the c/o latte, once Prince pays
        var card = Assert.Single(coffee.Cards);
        Assert.True(card.IsCurrent);
        Assert.Equal(new PublicSlotDto(1, true, new DateOnly(2026, 11, 2), "order"), card.Slots[0]);
        Assert.False(card.Slots[1].Filled);
    }

    // ---- claims ----

    [Fact]
    public async Task AClaim_MustFitTheTab_LessPendingClaims_WithAGoodReference_AndAnActiveMethod()
    {
        var (owner, anon, m) = await SetupAsync();
        var prince = await CreateCustomerAsync(owner, "Prince claims");
        await CreateOrderAsync(owner, OrderBody(prince.Id, [Line(m.Latte)]));        // 160
        await CreateOrderAsync(owner, OrderBody(prince.Id, [Line(m.Americano)]));    // 110 → 270
        var gcash = await MethodAsync(owner, "GCash claims");
        var off = await MethodAsync(owner, "BPI claims", PaymentChannel.BPI, active: false);
        var token = await LinkAsync(owner, prince.Id);

        Assert.Equal(HttpStatusCode.Conflict, (await ClaimAsync(anon, token, 270.01m, "GC12345678", gcash.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ClaimAsync(anon, token, 10m, "abc12", gcash.Id)).StatusCode);       // too short
        Assert.Equal(HttpStatusCode.BadRequest, (await ClaimAsync(anon, token, 10m, "GC-123456", gcash.Id)).StatusCode);   // not letters and digits
        Assert.Equal(HttpStatusCode.BadRequest, (await ClaimAsync(anon, token, 10m, new string('A', 33), gcash.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ClaimAsync(anon, token, 0m, "GC12345678", gcash.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ClaimAsync(anon, token, 10m, "BPI1234567", off.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ClaimAsync(anon, token, 10m, "BPI1234567", Guid.NewGuid())).StatusCode);

        var first = await ClaimAsync(anon, token, 200m, " GC12345678 ", gcash.Id);
        await EnsureAsync(first);
        var claim = (await first.Content.ReadFromJsonAsync<PublicClaimDto>(KloweeApiFactory.Json))!;
        Assert.Equal((200m, "GC12345678", PaymentClaimStatus.Pending, PaymentChannel.GCash), (claim.Amount, claim.Reference, claim.Status, claim.Channel));

        // The pending 200 lowers the limit to 70.
        Assert.Equal(HttpStatusCode.Conflict, (await ClaimAsync(anon, token, 70.01m, "GC22345678", gcash.Id)).StatusCode);
        await EnsureAsync(await ClaimAsync(anon, token, 70m, "GC22345678", gcash.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await ClaimAsync(anon, token, 1m, "GC32345678", gcash.Id)).StatusCode);

        var tab = await TabAsync(anon, token);
        Assert.Equal((270m, 270m), (tab.Balance, tab.PendingClaimsTotal));
        Assert.Equal([70m, 200m], tab.Claims.Select(c => c.Amount));   // newest first
        Assert.Equal(270m, tab.UnpaidOrders.Sum(o => o.Due));          // a claim moves no money

        var listed = await OwnerClaimAsync(owner, claim.Id);
        Assert.Equal(("Prince claims", 270m, "GCash claims"), (listed.CustomerName, listed.Outstanding, listed.PaymentMethodLabel));
    }

    [Fact]
    public async Task Confirm_SettlesOldestFirst_StampsTheCard_AndLinksThePayments()
    {
        var (owner, anon, m) = await SetupAsync();
        var prince = await CreateCardholderAsync(_factory, owner, "Prince confirm");
        var oldest = await CreateOrderAsync(owner, OrderBody(prince.Id, [Line(m.Latte)], orderedOn: new DateOnly(2026, 10, 1)));     // 160
        var newer = await CreateOrderAsync(owner, OrderBody(prince.Id, [Line(m.Americano)], orderedOn: new DateOnly(2026, 10, 8)));   // 110
        var gcash = await MethodAsync(owner, "GCash confirm");
        var token = await LinkAsync(owner, prince.Id);

        var response = await ClaimAsync(anon, token, 200m, "GC99887766", gcash.Id);
        var claim = (await response.Content.ReadFromJsonAsync<PublicClaimDto>(KloweeApiFactory.Json))!;
        var pendingBefore = (await SummaryAsync(owner)).PendingClaimsCount;
        Assert.True(pendingBefore >= 1);
        Assert.Contains((await owner.GetFromJsonAsync<List<PaymentClaimDto>>("/api/payment-claims?status=Pending", KloweeApiFactory.Json))!, c => c.Id == claim.Id);

        // The owner saw it landed in the business account: override the channel.
        var confirmed = await owner.PostAsJsonAsync($"/api/payment-claims/{claim.Id}/confirm", new { channel = "BusinessGCash" });
        await EnsureAsync(confirmed);
        var dto = (await confirmed.Content.ReadFromJsonAsync<PaymentClaimDto>(KloweeApiFactory.Json))!;
        Assert.Equal((PaymentClaimStatus.Confirmed, 70m), (dto.Status, dto.Outstanding));
        Assert.NotNull(dto.ResolvedAt);

        Assert.Equal(PaymentStatus.Paid, (await GetOrderAsync(owner, oldest.Id)).Status);
        Assert.Equal(PaymentStatus.Partial, (await GetOrderAsync(owner, newer.Id)).Status);
        Assert.Equal(1, await StampsAsync(owner, prince.Id));   // the paid latte stamped, through ReconcileAsync

        var payments = await _factory.WithDbAsync(db => db.Payments.Where(p => p.PaymentClaimId == claim.Id).OrderBy(p => p.Amount).ToListAsync());
        Assert.Equal([40m, 160m], payments.Select(p => p.Amount));
        Assert.All(payments, p => Assert.Equal((PaymentChannel.BusinessGCash, "GC99887766", Today), (p.Channel, p.Reference, p.PaidOn)));

        var tab = await TabAsync(anon, token);
        Assert.Equal((70m, 0m), (tab.Balance, tab.PendingClaimsTotal));
        Assert.Equal(PaymentClaimStatus.Confirmed, tab.Claims.Single().Status);
        Assert.Equal(pendingBefore - 1, (await SummaryAsync(owner)).PendingClaimsCount);

        // Once only.
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/payment-claims/{claim.Id}/confirm", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/payment-claims/{claim.Id}/reject", new { reason = "late" })).StatusCode);
    }

    [Fact]
    public async Task Confirm_AfterTheTabShrank_Is409_AndWritesNothing()
    {
        var (owner, anon, m) = await SetupAsync();
        var prince = await CreateCustomerAsync(owner, "Prince shrank");
        await CreateOrderAsync(owner, OrderBody(prince.Id, [Line(m.Latte)]));   // 160
        var gcash = await MethodAsync(owner, "GCash shrank");
        var token = await LinkAsync(owner, prince.Id);

        var claim = (await (await ClaimAsync(anon, token, 160m, "GC55554444", gcash.Id)).Content.ReadFromJsonAsync<PublicClaimDto>(KloweeApiFactory.Json))!;
        // Meanwhile Prince paid 100 in cash at the cart.
        await EnsureAsync(await owner.PostAsJsonAsync($"/api/customers/{prince.Id}/settle", PaymentBody(100m, PaymentChannel.Cash)));

        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/api/payment-claims/{claim.Id}/confirm", new { })).StatusCode);
        Assert.Equal(PaymentClaimStatus.Pending, (await OwnerClaimAsync(owner, claim.Id)).Status);
        Assert.Equal(0, await _factory.WithDbAsync(db => db.Payments.CountAsync(p => p.PaymentClaimId == claim.Id)));
        Assert.Equal(60m, (await GetCustomerAsync(owner, prince.Id)).OutstandingBalance);
    }

    [Fact]
    public async Task Reject_RecordsWhy_AndLeavesTheTabAlone()
    {
        var (owner, anon, m) = await SetupAsync();
        var prince = await CreateCustomerAsync(owner, "Prince reject");
        await CreateOrderAsync(owner, OrderBody(prince.Id, [Line(m.Latte)]));
        var gcash = await MethodAsync(owner, "GCash reject");
        var token = await LinkAsync(owner, prince.Id);
        var claim = (await (await ClaimAsync(anon, token, 160m, "GC11112222", gcash.Id)).Content.ReadFromJsonAsync<PublicClaimDto>(KloweeApiFactory.Json))!;

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync($"/api/payment-claims/{claim.Id}/reject", new { reason = "" })).StatusCode);
        var rejected = await owner.PostAsJsonAsync($"/api/payment-claims/{claim.Id}/reject", new { reason = "  No transfer with this reference.  " });
        await EnsureAsync(rejected);

        var tab = await TabAsync(anon, token);
        Assert.Equal((160m, 0m), (tab.Balance, tab.PendingClaimsTotal));
        var shown = tab.Claims.Single();
        Assert.Equal((PaymentClaimStatus.Rejected, "No transfer with this reference."), (shown.Status, shown.RejectReason));
        Assert.Equal(160m, (await GetCustomerAsync(owner, prince.Id)).OutstandingBalance);

        // With the rejected one out of the way, the full amount can be reported again.
        await EnsureAsync(await ClaimAsync(anon, token, 160m, "GC11113333", gcash.Id));
    }

    // ---- card requests ----

    [Fact]
    public async Task ACardRequest_MakesOneRequestedMembership_ApproveGivesTheCardAtZero_DeclineRemovesIt()
    {
        var (owner, anon, _) = await SetupAsync();
        var walkIn = await CreateCustomerAsync(owner, "Walk-in asks");
        var other = await CreateCustomerAsync(owner, "Walk-in declined");
        var token = await LinkAsync(owner, walkIn.Id);
        var otherToken = await LinkAsync(owner, other.Id);
        var countBefore = (await SummaryAsync(owner)).PendingCardRequestsCount;

        await EnsureAsync(await anon.PostAsync($"/api/public/tab/{token}/clubs/{LoyaltyClub.CoffeeClubId}/request", null));
        await EnsureAsync(await anon.PostAsync($"/api/public/tab/{token}/clubs/{LoyaltyClub.CoffeeClubId}/request", null));   // again: nothing new
        var rows = await _factory.WithDbAsync(db => db.LoyaltyMemberships.Where(x => x.CustomerId == walkIn.Id).ToListAsync());
        var request = Assert.Single(rows);
        Assert.Equal((MembershipStatus.Requested, (DateOnly?)null), (request.Status, request.JoinedOn));

        var club = (await TabAsync(anon, token)).Clubs.Single(c => c.ClubId == LoyaltyClub.CoffeeClubId);
        Assert.Equal("requested", club.Membership);
        Assert.NotNull(club.RequestedOn);
        Assert.Equal(countBefore + 1, (await SummaryAsync(owner)).PendingCardRequestsCount);

        var listed = Assert.Single((await owner.GetFromJsonAsync<List<CardRequestDto>>("/api/loyalty/card-requests", KloweeApiFactory.Json))!,
            r => r.CustomerId == walkIn.Id);
        Assert.Equal((request.Id, "Walk-in asks", "The Coffee Club"), (listed.MembershipId, listed.CustomerName, listed.ClubName));

        // Matcha isn't running, so it can't be asked for.
        Assert.Equal(HttpStatusCode.Conflict, (await anon.PostAsync($"/api/public/tab/{token}/clubs/{LoyaltyClub.MatchaClubId}/request", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.PostAsync($"/api/public/tab/{token}/clubs/{Guid.NewGuid()}/request", null)).StatusCode);

        // Approve: the card is live from today, at zero.
        await EnsureAsync(await owner.PostAsync($"/api/loyalty/memberships/{request.Id}/approve", null));
        var given = await _factory.WithDbAsync(db => db.LoyaltyMemberships.SingleAsync(x => x.CustomerId == walkIn.Id && x.ClubId == LoyaltyClub.CoffeeClubId));
        Assert.Equal((MembershipStatus.Active, (DateOnly?)Today), (given.Status, given.JoinedOn));
        club = (await TabAsync(anon, token)).Clubs.Single(c => c.ClubId == LoyaltyClub.CoffeeClubId);
        Assert.Equal(("member", 0), (club.Membership, club.Position));
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsync($"/api/loyalty/memberships/{request.Id}/approve", null)).StatusCode);

        // Asking as a member changes nothing.
        await EnsureAsync(await anon.PostAsync($"/api/public/tab/{token}/clubs/{LoyaltyClub.CoffeeClubId}/request", null));
        Assert.Single(await _factory.WithDbAsync(db => db.LoyaltyMemberships.Where(x => x.CustomerId == walkIn.Id).ToListAsync()));

        // Decline: the request goes.
        await EnsureAsync(await anon.PostAsync($"/api/public/tab/{otherToken}/clubs/{LoyaltyClub.CoffeeClubId}/request", null));
        var otherRequest = (await owner.GetFromJsonAsync<List<CardRequestDto>>("/api/loyalty/card-requests", KloweeApiFactory.Json))!.Single(r => r.CustomerId == other.Id);
        await EnsureAsync(await owner.PostAsync($"/api/loyalty/memberships/{otherRequest.MembershipId}/decline", null));
        Assert.DoesNotContain((await owner.GetFromJsonAsync<List<CardRequestDto>>("/api/loyalty/card-requests", KloweeApiFactory.Json))!, r => r.CustomerId == other.Id);
        Assert.Equal("none", (await TabAsync(anon, otherToken)).Clubs.Single(c => c.ClubId == LoyaltyClub.CoffeeClubId).Membership);
        Assert.Equal(countBefore, (await SummaryAsync(owner)).PendingCardRequestsCount);
    }

    [Fact]
    public async Task AnAutoJoinClub_ShowsWhichCardBringsIt_AndCannotBeAskedForOnItsOwn()
    {
        var (owner, anon, _) = await SetupAsync();
        var walkIn = await CreateCustomerAsync(owner, "Walk-in matcha");
        var token = await LinkAsync(owner, walkIn.Id);
        await EnsureAsync(await owner.PatchAsync($"/api/loyalty/clubs/{LoyaltyClub.MatchaClubId}/activate", null));
        try
        {
            var matcha = (await TabAsync(anon, token)).Clubs.Single(c => c.ClubId == LoyaltyClub.MatchaClubId);
            Assert.Equal(("none", (Guid?)LoyaltyClub.CoffeeClubId, "The Coffee Club"), (matcha.Membership, matcha.ComesWithClubId, matcha.ComesWithName));
            Assert.Equal(HttpStatusCode.Conflict, (await anon.PostAsync($"/api/public/tab/{token}/clubs/{LoyaltyClub.MatchaClubId}/request", null)).StatusCode);
        }
        finally
        {
            await EnsureAsync(await owner.PatchAsync($"/api/loyalty/clubs/{LoyaltyClub.MatchaClubId}/deactivate", null));
        }
    }

    // ---- headers, limits, logs ----

    [Fact]
    public async Task EveryTabResponse_IsNoStoreAndNoIndex_ErrorsIncluded()
    {
        var (owner, anon, _) = await SetupAsync();
        var kee = await CreateCustomerAsync(owner, "Kee headers");
        var token = await LinkAsync(owner, kee.Id);

        foreach (var response in new[]
                 {
                     await anon.GetAsync($"/api/public/tab/{token}"),
                     await anon.GetAsync("/api/public/tab/not-a-link"),
                     await anon.PostAsJsonAsync($"/api/public/tab/{token}/claims", new { amount = 1, reference = "x", paymentMethodId = Guid.NewGuid() })
                 })
        {
            Assert.True(response.Headers.CacheControl?.NoStore, $"{response.StatusCode}: no-store missing");
            Assert.Equal("noindex", Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
        }

        // Not on the rest of the API.
        Assert.False((await anon.GetAsync("/api/health")).Headers.Contains("X-Robots-Tag"));
    }

    [Fact]
    public async Task TheRealLimits_ReadsPerIp_ClaimsAndCardRequestsPerToken()
    {
        var (owner, _, _) = await SetupAsync();
        var a = await CreateCustomerAsync(owner, "Limit A");
        var b = await CreateCustomerAsync(owner, "Limit B");
        var tokenA = await LinkAsync(owner, a.Id);
        var tokenB = await LinkAsync(owner, b.Id);
        var gcash = await MethodAsync(owner, "GCash limits");

        using var limited = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<TabRateLimitOptions>(options =>
                {
                    var real = new TabRateLimitOptions();
                    options.ClaimsPerHour = real.ClaimsPerHour;
                    options.CardRequestsPerDay = real.CardRequestsPerDay;
                    options.ReadsPerMinute = 3;   // 60 in production; 3 keeps the test short
                })));
        var client = limited.CreateClient();

        // Claims: 5 an hour per token, counted whether they succeed or not.
        Assert.Equal(5, new TabRateLimitOptions().ClaimsPerHour);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await ClaimAsync(client, tokenA, 1m, "bad", gcash.Id)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await ClaimAsync(client, tokenA, 1m, "bad", gcash.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ClaimAsync(client, tokenB, 1m, "bad", gcash.Id)).StatusCode);   // another link has its own

        // Card requests: 3 a day per token.
        Assert.Equal(3, new TabRateLimitOptions().CardRequestsPerDay);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/public/tab/{tokenA}/clubs/{LoyaltyClub.CoffeeClubId}/request", null)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsync($"/api/public/tab/{tokenA}/clubs/{LoyaltyClub.CoffeeClubId}/request", null)).StatusCode);

        // Reads: per client IP, whichever link.
        Assert.Equal(60, new TabRateLimitOptions().ReadsPerMinute);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/public/tab/{tokenA}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/public/tab/{tokenB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/public/tab/guessing")).StatusCode);
        var limitedRead = await client.GetAsync($"/api/public/tab/{tokenA}");
        Assert.Equal(HttpStatusCode.TooManyRequests, limitedRead.StatusCode);
        Assert.True(limitedRead.Headers.CacheControl?.NoStore);

        var elsewhere = new HttpRequestMessage(HttpMethod.Get, $"/api/public/tab/{tokenA}");
        elsewhere.Headers.Add("CF-Connecting-IP", "203.0.113.9");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(elsewhere)).StatusCode);
    }

    [Fact]
    public async Task TokensNeverReachTheLogs_TheCustomerIdDoes()
    {
        var (owner, anon, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(owner, "Kee logs");
        await CreateOrderAsync(owner, OrderBody(kee.Id, [Line(m.Latte)]));
        var gcash = await MethodAsync(owner, "GCash logs");
        var token = await LinkAsync(owner, kee.Id);
        var guess = TabLinks.NewToken();

        await TabAsync(anon, token);
        await EnsureAsync(await ClaimAsync(anon, token, 50m, "GC77776666", gcash.Id));
        await ClaimAsync(anon, token, 5000m, "GC77776667", gcash.Id);   // 409
        await EnsureAsync(await anon.PostAsync($"/api/public/tab/{token}/clubs/{LoyaltyClub.CoffeeClubId}/request", null));
        await anon.GetAsync($"/api/public/tab/{guess}");                // 404
        await owner.GetAsync($"/api/customers/{kee.Id}");               // the admin sees the URL; it isn't logged either

        var lines = _factory.Logs.Lines;
        Assert.Contains(lines, l => l.Contains(kee.Id.ToString()) && l.Contains("Tab page read"));   // the capture works
        Assert.DoesNotContain(lines, l => l.Contains(token));
        Assert.DoesNotContain(lines, l => l.Contains(guess));
        Assert.DoesNotContain(lines, l => l.Contains("/api/public/tab/"));
    }

    [Fact]
    public void Redact_HidesTheTokenInAnyTabPath()
    {
        Assert.Equal("/api/public/tab/[token]", TabLinks.Redact("/api/public/tab/abc_DEF-123"));
        Assert.Equal("/api/public/tab/[token]/claims", TabLinks.Redact("/api/public/tab/abc_DEF-123/claims"));
        Assert.Equal("/api/customers/1", TabLinks.Redact("/api/customers/1"));
        Assert.Equal(string.Empty, TabLinks.Redact(null));
    }
}

/// <summary>The accounts customers pay into (handover 17), in their own database so the order is exact.</summary>
public class PaymentMethodTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public PaymentMethodTests(KloweeApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Methods_CreateReadUpdateDeactivateDelete_InSortOrderThenLabel()
    {
        var owner = await _factory.CreateOwnerClientAsync();
        async Task<PaymentMethodDto> Create(string label, string channel, int sortOrder, string? qr = null)
        {
            var response = await owner.PostAsJsonAsync("/api/payment-methods",
                new { channel, label, accountName = "Klowee Cafe", accountNumber = "123", qrUrl = qr, sortOrder });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<PaymentMethodDto>(KloweeApiFactory.Json))!;
        }

        Assert.Empty((await owner.GetFromJsonAsync<List<PaymentMethodDto>>("/api/payment-methods", KloweeApiFactory.Json))!);   // no seed

        var maya = await Create("Maya", "Maya", 2);
        var gcash = await Create("GCash", "GCash", 1, "https://fake.supabase.test/storage/v1/object/public/media/qr.png");
        var cimb = await Create("CIMB Bank", "CIMB", 1);
        Assert.True(maya.IsActive);

        async Task<List<string>> Labels() =>
            (await owner.GetFromJsonAsync<List<PaymentMethodDto>>("/api/payment-methods", KloweeApiFactory.Json))!.Select(x => x.Label).ToList();
        Assert.Equal(["CIMB Bank", "GCash", "Maya"], await Labels());

        var updated = await owner.PutAsJsonAsync($"/api/payment-methods/{maya.Id}",
            new { channel = "Maya", label = "Maya (Chloie)", accountName = "Chloie", sortOrder = 0 });
        await EnsureAsyncLocal(updated);
        Assert.Equal(["Maya (Chloie)", "CIMB Bank", "GCash"], await Labels());

        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/payment-methods",
            new { channel = "GCash", label = "", accountName = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/payment-methods",
            new { channel = "GCash", label = "QR", accountName = "x", qrUrl = "not a url" })).StatusCode);

        var off = await owner.PatchAsync($"/api/payment-methods/{gcash.Id}/deactivate", null);
        Assert.False((await off.Content.ReadFromJsonAsync<PaymentMethodDto>(KloweeApiFactory.Json))!.IsActive);
        var on = await owner.PatchAsync($"/api/payment-methods/{gcash.Id}/activate", null);
        Assert.True((await on.Content.ReadFromJsonAsync<PaymentMethodDto>(KloweeApiFactory.Json))!.IsActive);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/payment-methods/{cimb.Id}")).StatusCode);
        Assert.Equal(["Maya (Chloie)", "GCash"], await Labels());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync($"/api/payment-methods/{cimb.Id}",
            new { channel = "CIMB", label = "x", accountName = "x" })).StatusCode);
    }

    private static Task EnsureAsyncLocal(HttpResponseMessage response) => SalesFixture.EnsureAsync(response);
}
