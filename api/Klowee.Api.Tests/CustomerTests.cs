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
        await CreateCustomerAsync(client, "Duplicate Dee");

        var response = await client.PostAsJsonAsync("/api/customers", new { name = "Duplicate Dee" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task List_CarriesBalanceStampsRewardsAndLastOrder_AndSearchesNameAndNickname()
    {
        var (client, m) = await SetupAsync();
        var jo = await CreateCustomerAsync(client, "Josephine List", nickname: "Jojo");
        await CreateOrderAsync(client, OrderBody(jo.Id, [Line(m.Latte)], orderedOn: new DateOnly(2026, 11, 3)));
        var paid = await CreateOrderAsync(client, OrderBody(jo.Id, [Line(m.Americano, 11)], orderedOn: new DateOnly(2026, 11, 5)));
        await EnsureAsync(await PayAsync(client, paid.Id, paid.Total));

        var found = await client.GetFromJsonAsync<List<CustomerSummaryDto>>("/api/customers?q=jojo", KloweeApiFactory.Json);

        var row = Assert.Single(found!);
        Assert.Equal(160m, row.OutstandingBalance);          // the unpaid latte
        Assert.Equal(11, row.LoyaltyBalance);
        Assert.Equal(1, row.AvailableRewards);               // floor(11 / 10)
        Assert.Equal(new DateOnly(2026, 11, 5), row.LastOrderOn);
    }

    [Fact]
    public async Task Inactive_AreHiddenUnlessAskedFor()
    {
        var (client, _) = await SetupAsync();
        var quiet = await CreateCustomerAsync(client, "Quiet Inactive");
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
        var drinker = await CreateCustomerAsync(client, "Has Orders");
        var payer = await CreateCustomerAsync(client, "Pays For Others");
        await CreateOrderAsync(client, OrderBody(drinker.Id, [Line(m.Latte)], payerId: payer.Id));
        var nobody = await CreateCustomerAsync(client, "No Orders");

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/customers/{drinker.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/customers/{payer.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/customers/{nobody.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/customers/{nobody.Id}")).StatusCode);
    }

    [Fact]
    public async Task Merge_MovesOrdersPaymentsPayerReferencesAndLoyalty_ThenDeletesTheDuplicate()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee Merge");
        var dupe = await CreateCustomerAsync(client, "Kee (spreadsheet)");
        var prince = await CreateCustomerAsync(client, "Prince Merge");

        var own = await CreateOrderAsync(client, OrderBody(dupe.Id, [Line(m.Latte)]));
        await EnsureAsync(await PayAsync(client, own.Id, own.Total));                            // dupe earns 1 stamp
        var covered = await CreateOrderAsync(client, OrderBody(prince.Id, [Line(m.Americano)], payerId: dupe.Id));
        var keeCoversDupe = await CreateOrderAsync(client, OrderBody(dupe.Id, [Line(m.Matcha)], payerId: kee.Id));
        await GiveStampsAsync(client, dupe.Id, 4);

        var response = await client.PostAsync($"/api/customers/{dupe.Id}/merge-into/{kee.Id}", null);
        await EnsureAsync(response);
        var merged = (await response.Content.ReadFromJsonAsync<CustomerDetailDto>(KloweeApiFactory.Json))!;

        Assert.Equal(kee.Id, merged.Id);
        Assert.Equal(5, merged.LoyaltyBalance);                                                  // 1 earned + 4 adjusted
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
        var kee = await CreateCustomerAsync(client, "Kee Ledger");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte, 2)]));
        await EnsureAsync(await PayAsync(client, order.Id, order.Total));

        var response = await client.PostAsJsonAsync($"/api/customers/{kee.Id}/loyalty/adjust", new { delta = -1, notes = "spilled, re-made" });
        await EnsureAsync(response);

        var ledger = await LedgerAsync(client, kee.Id);
        Assert.Equal([LoyaltyReason.Adjustment, LoyaltyReason.Earned], ledger.Select(e => e.Reason));
        Assert.Equal("spilled, re-made", ledger[0].Notes);
        Assert.Equal(1, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/customers/{kee.Id}/loyalty/adjust", new { delta = 0 })).StatusCode);
    }

    [Fact]
    public async Task StampsPerReward_IsASetting_ValidatedAndKeptOffThePublicSite()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee Setting");
        await GiveStampsAsync(client, kee.Id, 6);
        Assert.Equal(0, (await GetCustomerAsync(client, kee.Id)).AvailableRewards);   // default 10

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync("/api/settings", new[] { new { key = "loyalty_stamps_per_reward", value = "0" } })).StatusCode);
        await EnsureAsync(await client.PutAsJsonAsync("/api/settings", new[] { new { key = "loyalty_stamps_per_reward", value = "5" } }));

        Assert.Equal(1, (await GetCustomerAsync(client, kee.Id)).AvailableRewards);

        // A redemption now costs 5.
        await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte)], DiscountType.LoyaltyFree));
        Assert.Equal(1, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);

        var home = await (await _factory.CreateAnonymousClientAsync())
            .GetFromJsonAsync<PublicHomeDto>("/api/public/home", KloweeApiFactory.Json);
        Assert.False(home!.Settings.ContainsKey("loyalty_stamps_per_reward"));

        await EnsureAsync(await client.PutAsJsonAsync("/api/settings", new[] { new { key = "loyalty_stamps_per_reward", value = "10" } }));
    }
}
