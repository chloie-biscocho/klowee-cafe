using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Customers;
using Klowee.Api.Contracts.Public;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using static Klowee.Api.Tests.SalesFixture;

namespace Klowee.Api.Tests;

public class CustomerTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public CustomerTests(KloweeApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, SalesMenu Menu)> SetupAsync() =>
        (await _factory.CreateOwnerClientAsync(), await SeedMenuAsync(_factory, "customers"));

    [Fact]
    public async Task ADuplicateName_Is409()
    {
        var (client, _) = await SetupAsync();
        await CreateCardholderAsync(_factory, client, "Duplicate Dee");

        var response = await client.PostAsJsonAsync("/api/customers", new { name = "Duplicate Dee" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task List_CarriesBalanceStampsRewardsAndLastOrder_AndSearchesNameAndNickname()
    {
        var (client, m) = await SetupAsync();
        var jo = await CreateCardholderAsync(_factory, client, "Josephine List", nickname: "Jojo");
        await CreateOrderAsync(client, OrderBody(jo.Id, [Line(m.Latte)], orderedOn: new DateOnly(2026, 11, 3)));
        var paid = await CreateOrderAsync(client, OrderBody(jo.Id, [Line(m.Americano, 11)], orderedOn: new DateOnly(2026, 11, 5)));
        await EnsureAsync(await PayAsync(client, paid.Id, paid.Total));

        var found = await client.GetFromJsonAsync<List<CustomerSummaryDto>>("/api/customers?q=jojo", KloweeApiFactory.Json);

        var row = Assert.Single(found!);
        Assert.Equal(160m, row.OutstandingBalance);          // the unpaid latte
        var coffee = Assert.Single(row.Clubs);                // only active clubs are listed
        Assert.Equal(LoyaltyClub.CoffeeClubId, coffee.ClubId);
        Assert.Equal(11, coffee.Position);
        Assert.Equal(1, coffee.PendingDrinks);              // the unpaid latte will stamp once paid
        Assert.Equal(["available", "available", "available"], // slots 5, 8 and 12: all earned, none used
            coffee.Rewards.Select(r => r.State));
        Assert.Equal(new DateOnly(2026, 11, 5), row.LastOrderOn);
    }

    [Fact]
    public async Task Inactive_AreHiddenUnlessAskedFor()
    {
        var (client, _) = await SetupAsync();
        var quiet = await CreateCardholderAsync(_factory, client, "Quiet Inactive");
        await client.PatchAsync($"/api/customers/{quiet.Id}/deactivate", null);

        var active = await client.GetFromJsonAsync<List<CustomerSummaryDto>>("/api/customers?q=Quiet", KloweeApiFactory.Json);
        var all = await client.GetFromJsonAsync<List<CustomerSummaryDto>>("/api/customers?q=Quiet&includeInactive=true", KloweeApiFactory.Json);

        Assert.Empty(active!);
        Assert.False(Assert.Single(all!).IsActive);
    }

    [Fact]
    public async Task Delete_IsRefusedWhileTheyHaveOrders_EvenAsPayer_AndAllowedOtherwise()
    {
        var (client, m) = await SetupAsync();
        var drinker = await CreateCardholderAsync(_factory, client, "Has Orders");
        var payer = await CreateCardholderAsync(_factory, client, "Pays For Others");
        await CreateOrderAsync(client, OrderBody(drinker.Id, [Line(m.Latte)], payerId: payer.Id));
        var nobody = await CreateCardholderAsync(_factory, client, "No Orders");

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/customers/{drinker.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/customers/{payer.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/customers/{nobody.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/customers/{nobody.Id}")).StatusCode);
    }

    [Fact]
    public async Task Merge_MovesOrdersPaymentsPayerReferencesAndLoyalty_ThenDeletesTheDuplicate()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee Merge");
        var dupe = await CreateCardholderAsync(_factory, client, "Kee (spreadsheet)");
        var prince = await CreateCardholderAsync(_factory, client, "Prince Merge");

        var own = await CreateOrderAsync(client, OrderBody(dupe.Id, [Line(m.Latte)]));
        await EnsureAsync(await PayAsync(client, own.Id, own.Total));                            // dupe earns 1 stamp
        var covered = await CreateOrderAsync(client, OrderBody(prince.Id, [Line(m.Americano)], payerId: dupe.Id));
        var keeCoversDupe = await CreateOrderAsync(client, OrderBody(dupe.Id, [Line(m.Matcha)], payerId: kee.Id));
        await GiveStampsAsync(client, dupe.Id, 4);

        var response = await client.PostAsync($"/api/customers/{dupe.Id}/merge-into/{kee.Id}", null);
        await EnsureAsync(response);
        var merged = (await response.Content.ReadFromJsonAsync<CustomerDetailDto>(KloweeApiFactory.Json))!;

        Assert.Equal(kee.Id, merged.Id);
        Assert.Equal(5, await StampsAsync(client, kee.Id));                                      // 1 earned + 4 adjusted
        Assert.Equal(110m + 150m, merged.OutstandingBalance);                                    // Prince's drink + the matcha

        var ownAfter = await GetOrderAsync(client, own.Id);
        Assert.Equal(kee.Id, ownAfter.Customer.Id);
        Assert.Single(ownAfter.Payments);                                                         // payments travel with the order
        Assert.Equal(kee.Id, (await GetOrderAsync(client, covered.Id)).Payer!.Id);
        Assert.Null((await GetOrderAsync(client, keeCoversDupe.Id)).Payer);                     // "Kee c/o Kee" is just Kee

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/customers/{dupe.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/customers/{kee.Id}/merge-into/{kee.Id}", null)).StatusCode);
    }

    [Fact]
    public async Task Ledger_IsNewestFirst_AndAdjustWritesAnAdjustment()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee Ledger");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte, 2)]));
        await EnsureAsync(await PayAsync(client, order.Id, order.Total));

        var response = await client.PostAsJsonAsync($"/api/customers/{kee.Id}/loyalty/adjust",
            new { clubId = LoyaltyClub.CoffeeClubId, delta = -1, notes = "spilled, re-made" });
        await EnsureAsync(response);

        var ledger = await LedgerAsync(client, kee.Id);
        Assert.Equal([LoyaltyReason.Adjustment, LoyaltyReason.Earned], ledger.Select(e => e.Reason));
        Assert.Equal("spilled, re-made", ledger[0].Notes);
        Assert.Equal(1, await StampsAsync(client, kee.Id));

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/customers/{kee.Id}/loyalty/adjust", new { clubId = LoyaltyClub.CoffeeClubId, delta = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,                                                   // which club? it must say
            (await client.PostAsJsonAsync($"/api/customers/{kee.Id}/loyalty/adjust", new { delta = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,                                                     // not launched yet
            (await client.PostAsJsonAsync($"/api/customers/{kee.Id}/loyalty/adjust", new { clubId = LoyaltyClub.MatchaClubId, delta = 1 })).StatusCode);
    }

    [Fact]
    public async Task StampsPerReward_IsRetired_AndUnknownToTheSettings()
    {
        var (client, _) = await SetupAsync();

        var response = await client.PutAsJsonAsync("/api/settings", new[] { new { key = "loyalty_stamps_per_reward", value = "10" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
