using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Loyalty;
using Klowee.Api.Entities;
using static Klowee.Api.Tests.SalesFixture;

namespace Klowee.Api.Tests;

/// <summary>
/// Rules 1 and 2 with a second club: which club a drink stamps, and that an
/// inactive club earns nothing until it is activated, with no backfill.
/// Its own database, because these tests switch The Matcha Club on and off.
/// </summary>
public class LoyaltyMatchaClubTests : IClassFixture<KloweeApiFactory>
{
    private const string Suffix = "matcha";
    private readonly KloweeApiFactory _factory;

    public LoyaltyMatchaClubTests(KloweeApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, SalesMenu Menu)> SetupAsync() =>
        (await _factory.CreateOwnerClientAsync(), await SeedMenuAsync(_factory, Suffix));

    private static async Task SetMatchaAsync(HttpClient client, bool active) =>
        await EnsureAsync(await client.PatchAsync(
            $"/api/loyalty/clubs/{LoyaltyClub.MatchaClubId}/{(active ? "activate" : "deactivate")}", null));

    [Fact]
    public async Task AnInactiveClub_EarnsNothingAndIsNotShown_UntilActivated_WithNoBackfill()
    {
        var (client, m) = await SetupAsync();
        await SetMatchaAsync(client, false);
        var okumidori = await AddDrinkAsync(_factory, Suffix, "Okumidori Latte", 180m, categoryClub: LoyaltyClub.MatchaClubId);
        var kee = await CreateCardholderAsync(_factory, client, "Kee before launch");

        var before = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(okumidori)], orderedOn: Today.AddDays(-1)));
        await EnsureAsync(await PayAsync(client, before.Id, before.Total));
        Assert.Empty(await LedgerAsync(client, kee.Id));
        Assert.DoesNotContain((await GetCustomerAsync(client, kee.Id)).Clubs, c => c.ClubId == LoyaltyClub.MatchaClubId);

        // Launch day: dated today (the business time zone's), shown on customers.
        await SetMatchaAsync(client, true);
        var club = (await client.GetFromJsonAsync<LoyaltyClubDto>($"/api/loyalty/clubs/{LoyaltyClub.MatchaClubId}", KloweeApiFactory.Json))!;
        Assert.Equal(Today, club.ActivatedOn);
        Assert.Equal(0, await StampsAsync(client, kee.Id, LoyaltyClub.MatchaClubId));   // yesterday's drink isn't backfilled

        // An order from before launch, paid after it, still doesn't stamp; today's does.
        var late = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(okumidori)], orderedOn: Today.AddDays(-2)));
        await EnsureAsync(await PayAsync(client, late.Id, late.Total));
        var today = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(okumidori)]));
        await EnsureAsync(await PayAsync(client, today.Id, today.Total));
        Assert.Equal(1, await StampsAsync(client, kee.Id, LoyaltyClub.MatchaClubId));
    }

    [Fact]
    public async Task OneOrder_StampsEachClubItsOwnDrinks_ItemOverridesWinning()
    {
        var (client, m) = await SetupAsync();
        await SetMatchaAsync(client, true);
        var houseblend = await AddDrinkAsync(_factory, Suffix, "Houseblend Latte", 160m, categoryClub: LoyaltyClub.MatchaClubId);
        // Dirty Matcha sits in a coffee category but stamps The Matcha Club.
        var dirty = await AddDrinkAsync(_factory, Suffix, "Dirty Matcha", 170m,
            categoryClub: LoyaltyClub.CoffeeClubId, mode: LoyaltyMode.Club, itemClub: LoyaltyClub.MatchaClubId);
        // A coffee-category drink switched off: earns nothing.
        var tasting = await AddDrinkAsync(_factory, Suffix, "Tasting cup", 50m,
            categoryClub: LoyaltyClub.CoffeeClubId, mode: LoyaltyMode.None);
        // Given the Coffee Club card today, while The Matcha Club is active: both cards.
        var kee = await CreateCustomerAsync(client, "Kee two cards");
        await EnsureAsync(await client.PostAsJsonAsync($"/api/customers/{kee.Id}/loyalty/memberships", new { clubId = LoyaltyClub.CoffeeClubId }));

        var order = await CreateOrderAsync(client, OrderBody(kee.Id,
            [Line(m.Latte, 2), Line(houseblend), Line(dirty), Line(tasting, 4)]));
        await EnsureAsync(await PayAsync(client, order.Id, order.Total));

        var earned = (await LedgerAsync(client, kee.Id)).ToDictionary(e => e.ClubId, e => e.Delta);
        Assert.Equal(2, earned[LoyaltyClub.CoffeeClubId]);
        Assert.Equal(2, earned[LoyaltyClub.MatchaClubId]);
        Assert.Equal(2, earned.Count);
    }

    [Fact]
    public async Task ACoffeeReward_CannotBeUsedOnAMatchaDrink()
    {
        var (client, _) = await SetupAsync();
        await SetMatchaAsync(client, true);
        var matcha = await AddDrinkAsync(_factory, Suffix, "Ceremonial", 200m, categoryClub: LoyaltyClub.MatchaClubId);
        var kee = await CreateCardholderAsync(_factory, client, "Kee coffee reward");
        await GiveStampsAsync(client, kee.Id, 11);

        var response = await PostOrderAsync(client, OrderBody(kee.Id, [RewardLine(matcha, await CoffeeRewardAsync(_factory, 12))]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
