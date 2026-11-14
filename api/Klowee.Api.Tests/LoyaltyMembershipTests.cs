using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Customers;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using static Klowee.Api.Tests.SalesFixture;

namespace Klowee.Api.Tests;

/// <summary>
/// Cards by invitation (handover 16a): only cardholders collect stamps, a card
/// stamps from the day it was given, and cards that come with another card.
/// Its own database, because these tests switch The Matcha Club on and off.
/// </summary>
public class LoyaltyMembershipTests : IClassFixture<KloweeApiFactory>
{
    private const string Suffix = "members";
    private readonly KloweeApiFactory _factory;

    public LoyaltyMembershipTests(KloweeApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, SalesMenu Menu)> SetupAsync() =>
        (await _factory.CreateOwnerClientAsync(), await SeedMenuAsync(_factory, Suffix));

    private static Task<HttpResponseMessage> GiveCardAsync(HttpClient client, Guid customerId, Guid clubId) =>
        client.PostAsJsonAsync($"/api/customers/{customerId}/loyalty/memberships", new { clubId });

    private Task<List<LoyaltyMembership>> CardsAsync(Guid customerId, bool includeTaken = false) =>
        _factory.WithDbAsync(db => (includeTaken ? db.LoyaltyMemberships.IgnoreQueryFilters() : db.LoyaltyMemberships)
            .Where(m => m.CustomerId == customerId).ToListAsync());

    private async Task<Guid> PaidOrderAsync(HttpClient client, Guid customerId, Guid drink, DateOnly? on = null)
    {
        var order = await CreateOrderAsync(client, OrderBody(customerId, [Line(drink)], orderedOn: on));
        await EnsureAsync(await PayAsync(client, order.Id, order.Total));
        return order.Id;
    }

    private static async Task SetMatchaAsync(HttpClient client, bool active) =>
        await EnsureAsync(await client.PatchAsync(
            $"/api/loyalty/clubs/{LoyaltyClub.MatchaClubId}/{(active ? "activate" : "deactivate")}", null));

    [Fact]
    public async Task ANonMember_EarnsNothing_AndShowsNoCard()
    {
        var (client, m) = await SetupAsync();
        var walkIn = await CreateCustomerAsync(client, "Walk-in no card");

        await PaidOrderAsync(client, walkIn.Id, m.Latte);

        Assert.Empty(await LedgerAsync(client, walkIn.Id));
        var card = await CardAsync(client, walkIn.Id);
        Assert.Equal(("none", (Guid?)null, 0, 0), (card.Membership, card.MembershipId, card.Position, card.PendingDrinks));
        Assert.Empty(card.Rewards);
    }

    [Fact]
    public async Task AGivenCard_StampsFromToday_NotYesterday_AndShowsPendingOnlyForWhatWillStamp()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee new card");
        await PaidOrderAsync(client, kee.Id, m.Latte, Today.AddDays(-1));
        await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano)], orderedOn: Today.AddDays(-1)));   // an old unpaid one

        var response = await GiveCardAsync(client, kee.Id, LoyaltyClub.CoffeeClubId);
        await EnsureAsync(response);
        var given = (await response.Content.ReadFromJsonAsync<LoyaltyMembershipDto>(KloweeApiFactory.Json))!;
        Assert.Equal((MembershipStatus.Active, (DateOnly?)Today), (given.Status, given.JoinedOn));

        var card = await CardAsync(client, kee.Id);
        Assert.Equal(("member", 0, 0), (card.Membership, card.Position, card.PendingDrinks));   // yesterday's drinks don't count
        await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano)]));
        Assert.Equal(1, (await CardAsync(client, kee.Id)).PendingDrinks);

        await PaidOrderAsync(client, kee.Id, m.Latte);
        Assert.Equal(1, await StampsAsync(client, kee.Id));
    }

    [Fact]
    public async Task ARewardForANonMember_Is409()
    {
        var (client, m) = await SetupAsync();
        var walkIn = await CreateCustomerAsync(client, "Walk-in reward");

        var response = await PostOrderAsync(client, OrderBody(walkIn.Id, [RewardLine(m.Latte, await CoffeeRewardAsync(_factory, 12))]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,      // and stamps can't be adjusted onto a card they don't hold
            (await client.PostAsJsonAsync($"/api/customers/{walkIn.Id}/loyalty/adjust", new { clubId = LoyaltyClub.CoffeeClubId, delta = 3 })).StatusCode);
    }

    [Fact]
    public async Task GivingTwice_KeepsOneCard_AndTakingAwayNeedsForceOnceItHasStamps()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee take away");
        await EnsureAsync(await GiveCardAsync(client, kee.Id, LoyaltyClub.CoffeeClubId));
        await EnsureAsync(await GiveCardAsync(client, kee.Id, LoyaltyClub.CoffeeClubId));
        var card = Assert.Single(await CardsAsync(kee.Id));
        var url = $"/api/customers/{kee.Id}/loyalty/memberships/{card.Id}";

        await PaidOrderAsync(client, kee.Id, m.Latte);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync(url)).StatusCode);
        Assert.Single(await CardsAsync(kee.Id));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{url}?force=true")).StatusCode);
        Assert.Empty(await CardsAsync(kee.Id));
        Assert.Empty(await LedgerAsync(client, kee.Id));                        // the stamps were voided
        Assert.Equal("none", (await CardAsync(client, kee.Id)).Membership);

        // An empty card goes without force.
        var walkIn = await CreateCustomerAsync(client, "Walk-in empty card");
        await EnsureAsync(await GiveCardAsync(client, walkIn.Id, LoyaltyClub.CoffeeClubId));
        var empty = Assert.Single(await CardsAsync(walkIn.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/customers/{walkIn.Id}/loyalty/memberships/{empty.Id}")).StatusCode);
    }

    [Fact]
    public async Task Merge_MovesCards_TheEarlierOneWinning()
    {
        var (client, _) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee merge cards");
        var dupe = await CreateCardholderAsync(_factory, client, "Kee merge cards (sheet)");    // since the beginning
        await EnsureAsync(await GiveCardAsync(client, kee.Id, LoyaltyClub.CoffeeClubId));       // since today

        await EnsureAsync(await client.PostAsync($"/api/customers/{dupe.Id}/merge-into/{kee.Id}", null));

        var card = Assert.Single(await CardsAsync(kee.Id));
        Assert.Null(card.JoinedOn);
        Assert.Equal(2, (await CardsAsync(kee.Id, includeTaken: true)).Count + (await CardsAsync(dupe.Id, includeTaken: true)).Count);
    }

    [Fact]
    public async Task CardsThatComeWithAnother_FollowTheMatchaLaunch()
    {
        var (client, m) = await SetupAsync();
        await SetMatchaAsync(client, false);
        var early = await CreateCardholderAsync(_factory, client, "Coffee member before launch");
        var walkIn = await CreateCustomerAsync(client, "No card at launch");

        // Before the launch: a Coffee card only, and Matcha shows nowhere.
        var beforeLaunch = await CreateCustomerAsync(client, "Card before launch");
        await EnsureAsync(await GiveCardAsync(client, beforeLaunch.Id, LoyaltyClub.CoffeeClubId));
        Assert.Equal([LoyaltyClub.CoffeeClubId], (await CardsAsync(beforeLaunch.Id)).Select(c => c.ClubId));
        Assert.Equal(HttpStatusCode.Conflict, (await GiveCardAsync(client, walkIn.Id, LoyaltyClub.MatchaClubId)).StatusCode);

        // Launch: every Coffee member gets a Matcha card dated that day; a non-member doesn't.
        _factory.Clock.Today = Today.AddDays(3);
        try
        {
            await SetMatchaAsync(client, true);
            foreach (var member in new[] { early.Id, beforeLaunch.Id })
            {
                var matcha = Assert.Single(await CardsAsync(member), c => c.ClubId == LoyaltyClub.MatchaClubId);
                Assert.Equal(Today.AddDays(3), matcha.JoinedOn);
            }

            Assert.Empty(await CardsAsync(walkIn.Id));

            // Running it again adds nothing.
            var before = (await _factory.WithDbAsync(db => db.LoyaltyMemberships.CountAsync()));
            await SetMatchaAsync(client, true);
            Assert.Equal(before, await _factory.WithDbAsync(db => db.LoyaltyMemberships.CountAsync()));

            // After the launch, a Coffee card brings the Matcha card with it.
            await EnsureAsync(await GiveCardAsync(client, walkIn.Id, LoyaltyClub.CoffeeClubId));
            Assert.Equal(new[] { LoyaltyClub.CoffeeClubId, LoyaltyClub.MatchaClubId }.Order(), (await CardsAsync(walkIn.Id)).Select(c => c.ClubId).Order());

            // Taking the Coffee card away leaves the Matcha card.
            var coffee = (await CardsAsync(walkIn.Id)).Single(c => c.ClubId == LoyaltyClub.CoffeeClubId);
            await EnsureAsync(await client.DeleteAsync($"/api/customers/{walkIn.Id}/loyalty/memberships/{coffee.Id}"));
            Assert.Equal([LoyaltyClub.MatchaClubId], (await CardsAsync(walkIn.Id)).Select(c => c.ClubId));
        }
        finally
        {
            _factory.Clock.Today = Today;
            await SetMatchaAsync(client, false);
        }
    }

    [Fact]
    public async Task AClub_CannotComeWithItself_OrInALoop()
    {
        var (client, _) = await SetupAsync();
        var body = (Guid? from) => new
        {
            name = "The Coffee Club", style = "Coffee", slotCount = 12, isActive = true, sortOrder = 1,
            autoJoinFromClubId = from,
            rewards = Array.Empty<object>()
        };

        // Coffee from Coffee: itself. Coffee from Matcha, while Matcha comes from Coffee: a loop.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/loyalty/clubs/{LoyaltyClub.CoffeeClubId}", body(LoyaltyClub.CoffeeClubId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/loyalty/clubs/{LoyaltyClub.CoffeeClubId}", body(LoyaltyClub.MatchaClubId))).StatusCode);
    }

    [Fact]
    public async Task TheCustomersCards_CarryEachStampsOrderDate_AndTheCarryOverUndated()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee dated stamps");
        await GiveStampsAsync(client, kee.Id, 11);
        var order = await PaidOrderAsync(client, kee.Id, m.Latte, Today.AddDays(-2));
        var second = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano, 2)]));
        await EnsureAsync(await PayAsync(client, second.Id, second.Total));

        var card = await CardAsync(client, kee.Id);

        Assert.Equal([(1, false), (2, true)], card.Cards.Select(c => (c.CardNumber, c.IsCurrent)));
        var first = card.Cards[0];
        Assert.All(first.Slots.Take(11), s => Assert.Equal(("adjustment", (DateOnly?)null), (s.Source, s.Date)));
        Assert.Equal(("order", (DateOnly?)Today.AddDays(-2), (Guid?)order), (first.Slots[11].Source, first.Slots[11].Date, first.Slots[11].OrderId));
        Assert.Equal(3, first.Rewards.Count);                                     // every reward of card 1, not only the available ones
        var now = card.Cards[1];
        Assert.Equal([(1, (DateOnly?)Today), (2, Today)], now.Slots.Where(s => s.Filled).Select(s => (s.Slot, s.Date)));
        Assert.Equal((card.Cycle, card.Position), (now.CardNumber - 1, now.Slots.Count(s => s.Filled)));
    }

    [Fact]
    public async Task AVoidedEntry_LeavesNoSlot()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee voided stamp");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte, 2)]));
        await EnsureAsync(await PayAsync(client, order.Id, order.Total));
        var payment = (await GetOrderAsync(client, order.Id)).Payments.Single();
        await EnsureAsync(await client.DeleteAsync($"/api/orders/{order.Id}/payments/{payment.Id}"));   // unpaid: its stamps are voided

        var card = Assert.Single((await CardAsync(client, kee.Id)).Cards);

        Assert.DoesNotContain(card.Slots, s => s.Filled);
    }
}
