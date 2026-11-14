using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Loyalty;
using Klowee.Api.Contracts.Reports;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using static Klowee.Api.Tests.SalesFixture;

namespace Klowee.Api.Tests;

/// <summary>
/// Handover 17a: a carried-over card never stamps an order dated on or before
/// the carry-over day (on paper, those drinks were stamped when ordered).
/// Here the carry-over day is <see cref="SalesFixture.Today"/>.
/// </summary>
public class LoyaltyStampsAfterTests : IClassFixture<KloweeApiFactory>
{
    private const string Suffix = "after";
    private readonly KloweeApiFactory _factory;

    public LoyaltyStampsAfterTests(KloweeApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, SalesMenu Menu)> SetupAsync() =>
        (await _factory.CreateOwnerClientAsync(), await SeedMenuAsync(_factory, Suffix));

    /// <summary>A Coffee Club card as the carry-over writes it: since the beginning, paper position taken on Today.</summary>
    private async Task<Guid> CarriedAsync(HttpClient client, string name)
    {
        var customer = await CreateCardholderAsync(_factory, client, name);
        await SetStampsAfterAsync(customer.Id, Today);
        return customer.Id;
    }

    private Task SetStampsAfterAsync(Guid customerId, DateOnly? day) =>
        _factory.WithDbAsync(async db =>
        {
            var card = await db.LoyaltyMemberships.SingleAsync(m => m.CustomerId == customerId && m.ClubId == LoyaltyClub.CoffeeClubId);
            card.StampsAfter = day;
            return await db.SaveChangesAsync();
        });

    private async Task<Guid> PaidOrderAsync(HttpClient client, Guid customer, Guid drink, DateOnly on)
    {
        var order = await CreateOrderAsync(client, OrderBody(customer, [Line(drink)], orderedOn: on));
        await EnsureAsync(await PayAsync(client, order.Id, order.Total));
        return order.Id;
    }

    private static async Task<StrayStampsDto> StrayAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<StrayStampsDto>("/api/loyalty/stray-stamps", KloweeApiFactory.Json))!;

    [Fact]
    public async Task AnOldUnpaidOrder_PaidLater_DoesNotStamp_TheDayAfterDoes()
    {
        var (client, m) = await SetupAsync();
        var dean = await CarriedAsync(client, "Dean after");

        var onTheDay = await CreateOrderAsync(client, OrderBody(dean, [Line(m.Latte)], orderedOn: Today));
        var before = await CreateOrderAsync(client, OrderBody(dean, [Line(m.Americano)], orderedOn: Today.AddDays(-20)));
        var after = await CreateOrderAsync(client, OrderBody(dean, [Line(m.Matcha)], orderedOn: Today.AddDays(1)));

        // Only the order after the carry-over day is waiting to stamp.
        Assert.Equal(1, (await CardAsync(client, dean)).PendingDrinks);

        await EnsureAsync(await PayAsync(client, onTheDay.Id, onTheDay.Total));
        await EnsureAsync(await PayAsync(client, before.Id, before.Total));
        Assert.Equal(0, await StampsAsync(client, dean));
        Assert.Empty(await LedgerAsync(client, dean));

        await EnsureAsync(await PayAsync(client, after.Id, after.Total));
        Assert.Equal(1, await StampsAsync(client, dean));
        Assert.Equal(Today.AddDays(1), (await CardAsync(client, dean)).Cards.Single().Slots[0].Date);
        Assert.Equal(0, (await CardAsync(client, dean)).PendingDrinks);
    }

    [Fact]
    public async Task ACardGivenByHand_StillStampsAnOrderOnItsFirstDay()
    {
        var (client, m) = await SetupAsync();
        var walkIn = await CreateCustomerAsync(client, "Walk-in after");
        await EnsureAsync(await client.PostAsJsonAsync($"/api/customers/{walkIn.Id}/loyalty/memberships", new { clubId = LoyaltyClub.CoffeeClubId }));
        Assert.Null((await _factory.WithDbAsync(db => db.LoyaltyMemberships.SingleAsync(x => x.CustomerId == walkIn.Id))).StampsAfter);

        await PaidOrderAsync(client, walkIn.Id, m.Latte, Today);

        Assert.Equal(1, await StampsAsync(client, walkIn.Id));
    }

    [Fact]
    public async Task StampsGivenByMistake_AreListed_ThenVoidedByReconcile_AndMoneyNeverMoves()
    {
        var (client, m) = await SetupAsync();
        // Before 17a: no StampsAfter, so the old order stamped when paid; the later one rightly.
        var sheki = (await CreateCardholderAsync(_factory, client, "Sheki after")).Id;
        await GiveStampsAsync(client, sheki, 7);                       // the paper position
        var old = await PaidOrderAsync(client, sheki, m.Latte, Today.AddDays(-3));
        var later = await PaidOrderAsync(client, sheki, m.Americano, Today.AddDays(2));
        Assert.Equal(9, await StampsAsync(client, sheki));

        await SetStampsAfterAsync(sheki, Today);                       // what the backfill does
        var listed = await StrayAsync(client);
        var stamp = Assert.Single(listed.Stamps, s => s.CustomerId == sheki);
        Assert.Equal((old, Today.AddDays(-3), 1), (stamp.OrderId, stamp.OrderedOn, stamp.Stamps));
        var position = Assert.Single(listed.Positions, p => p.CustomerName == "Sheki after");
        Assert.Equal(("card 1, 9 of 12", "card 1, 8 of 12"), (position.Before, position.After));
        Assert.Equal(9, await StampsAsync(client, sheki));             // listing changes nothing

        var summary = await client.GetStringAsync($"/api/reports/summary?from=2026-01-01&to=2026-12-31");
        var outstanding = await client.GetStringAsync("/api/reports/outstanding");
        var payments = await _factory.WithDbAsync(db => db.Payments.CountAsync());

        var voided = await client.PostAsync("/api/loyalty/stray-stamps/void", null);
        await EnsureAsync(voided);

        Assert.DoesNotContain((await StrayAsync(client)).Stamps, s => s.CustomerId == sheki);
        Assert.Equal(8, await StampsAsync(client, sheki));              // the later order keeps its stamp
        Assert.Contains(await LedgerAsync(client, sheki), e => e.OrderId == later && e.Delta == 1);
        Assert.Equal(summary, await client.GetStringAsync($"/api/reports/summary?from=2026-01-01&to=2026-12-31"));
        Assert.Equal(outstanding, await client.GetStringAsync("/api/reports/outstanding"));
        Assert.Equal(payments, await _factory.WithDbAsync(db => db.Payments.CountAsync()));
    }

    [Fact]
    public async Task ARewardThatOnlyTheStrayStampUnlocked_IsListed_AndItsOrderIsNotUndone()
    {
        var (client, m) = await SetupAsync();
        var june = (await CreateCardholderAsync(_factory, client, "June after")).Id;
        await GiveStampsAsync(client, june, 3);
        await PaidOrderAsync(client, june, m.Latte, Today.AddDays(-1)); // 4: the 50% off on slot 5 became available
        var halfOff = await CoffeeRewardAsync(_factory, 5);
        var usedIt = await CreateOrderAsync(client, OrderBody(june, [RewardLine(m.Latte, halfOff)], orderedOn: Today.AddDays(1)));

        await SetStampsAfterAsync(june, Today);
        var listed = await StrayAsync(client);
        var reward = Assert.Single(listed.UsedRewards, r => r.CustomerName == "June after");
        Assert.Equal(("50% OFF", 1, 5, usedIt.Id), (reward.Reward, reward.CardNumber, reward.Slot, reward.OrderId));

        await EnsureAsync(await client.PostAsync("/api/loyalty/stray-stamps/void", null));

        var order = await GetOrderAsync(client, usedIt.Id);
        Assert.Equal(usedIt.Total, order.Total);
        Assert.NotNull(order.Items.Single().Reward);
        Assert.Equal(3, await StampsAsync(client, june));
    }

    [Fact]
    public async Task TheStrayStampEndpoints_AreOwnersOnly()
    {
        var anon = await _factory.CreateAnonymousClientAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/loyalty/stray-stamps")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/loyalty/stray-stamps/void", null)).StatusCode);
    }
}
