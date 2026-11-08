using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using static Klowee.Api.Tests.SalesFixture;

namespace Klowee.Api.Tests;

public class OrderTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;
    public OrderTests(KloweeApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, SalesMenu Menu)> SetupAsync()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var menu = await SeedMenuAsync(_factory, "orders");
        return (client, menu);
    }

    [Fact]
    public async Task Create_SnapshotsPrices_AndStoresTheTotals()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee snapshot");

        var order = await CreateOrderAsync(client, OrderBody(kee.Id,
            [Line(m.Latte, 2, m.OatMilk), Line(m.Americano)]));

        // (160 + 30) × 2 + 110
        Assert.Equal(490m, order.Subtotal);
        Assert.Equal(490m, order.Total);
        Assert.Equal([160m, 110m], order.Items.Select(i => i.UnitPrice));
        Assert.Equal(30m, order.Items[0].AddOns.Single().UnitPrice);
        Assert.Equal(PaymentStatus.Unpaid, order.Status);
        Assert.Empty(order.Warnings);
    }

    [Fact]
    public async Task Repricing_TheMenu_NeverChangesAnExistingOrder()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee repricing");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano)]));

        await _factory.WithDbAsync(async db =>
        {
            var row = await db.MenuVersionItems.SingleAsync(vi => vi.Id == m.Americano);
            row.Price = 999m;
            return await db.SaveChangesAsync();
        });

        var reread = await GetOrderAsync(client, order.Id);
        Assert.Equal(110m, reread.Items.Single().UnitPrice);
        Assert.Equal(110m, reread.Total);

        await _factory.WithDbAsync(async db =>
        {
            (await db.MenuVersionItems.SingleAsync(vi => vi.Id == m.Americano)).Price = 110m;
            return await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Put_KeepsTheSnapshotOfALineThatStays_AndPricesANewLineNow()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee put snapshot");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Matcha, 1, m.ExtraShot)]));

        // The menu moves on after the sale...
        await _factory.WithDbAsync(async db =>
        {
            (await db.MenuVersionItems.SingleAsync(vi => vi.Id == m.Matcha)).Price = 175m;
            (await db.AddOns.SingleAsync(a => a.Id == m.ExtraShot)).Price = 40m;
            return await db.SaveChangesAsync();
        });

        // ...and the owner fixes the quantity and adds a drink.
        var response = await client.PutAsJsonAsync($"/api/orders/{order.Id}",
            OrderBody(kee.Id, [Line(m.Matcha, 2, m.ExtraShot), Line(m.Americano)]));
        await EnsureAsync(response);
        var updated = (await response.Content.ReadFromJsonAsync<OrderDetailDto>(KloweeApiFactory.Json))!;

        Assert.Equal(150m, updated.Items[0].UnitPrice);              // sold at 150, stays 150
        Assert.Equal(25m, updated.Items[0].AddOns.Single().UnitPrice);
        Assert.Equal(110m, updated.Items[1].UnitPrice);              // new line, today's price
        Assert.Equal((150m + 25m) * 2 + 110m, updated.Total);

        await _factory.WithDbAsync(async db =>
        {
            (await db.MenuVersionItems.SingleAsync(vi => vi.Id == m.Matcha)).Price = 150m;
            (await db.AddOns.SingleAsync(a => a.Id == m.ExtraShot)).Price = 25m;
            return await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task AnItemFromAnotherVersion_IsAllowed_WithAWarning()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee warning");

        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.OldPopUpLatte), Line(m.Americano)]));

        Assert.Equal(140m, order.Items[0].UnitPrice);
        var warning = Assert.Single(order.Warnings);
        Assert.Contains("not the current office menu", warning);
    }

    [Fact]
    public async Task PercentDiscount_RoundsHalfUp_AndOutOfRangeIs400()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee percent");

        // 145? No such price here: 110 × 12.5% = 13.75 exactly; use 3 Americanos: 330 × 12.5% = 41.25.
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano, 3)], DiscountType.Percent, 12.5m));
        Assert.Equal(41.25m, order.DiscountAmount);
        Assert.Equal(288.75m, order.Total);

        var tooMuch = await PostOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano)], DiscountType.Percent, 101m));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
    }

    [Fact]
    public async Task FixedDiscount_AboveTheSubtotal_Is400()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee fixed");

        var ok = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano)], DiscountType.Fixed, 110m));
        Assert.Equal(0m, ok.Total);

        var tooMuch = await PostOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano)], DiscountType.Fixed, 110.01m));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
    }

    [Fact]
    public async Task LoyaltyFree_WithoutAReward_Is409()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee no reward");
        await GiveStampsAsync(client, kee.Id, 9);

        var response = await PostOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte)], DiscountType.LoyaltyFree));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task LoyaltyFree_FreesTheCheapestDrink_RedeemsStamps_AndRemovingItReversesThat()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee free");
        await GiveStampsAsync(client, kee.Id, 12);

        var order = await CreateOrderAsync(client, OrderBody(kee.Id,
            [Line(m.Latte), Line(m.Americano, 1, m.OatMilk)], DiscountType.LoyaltyFree, discountValue: 50m));

        // Per unit: Latte 160, Americano + oat 140. The 140 one is free.
        Assert.Equal(140m, order.DiscountAmount);
        Assert.Equal(160m, order.Total);
        Assert.Equal(0m, order.DiscountValue);
        var redeemed = Assert.Single(await LedgerAsync(client, kee.Id), e => e.Reason == LoyaltyReason.Redeemed);
        Assert.Equal(-10, redeemed.Delta);
        Assert.Equal(2, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);

        // Re-saving with the discount still on doesn't need a second reward.
        var again = await client.PutAsJsonAsync($"/api/orders/{order.Id}",
            OrderBody(kee.Id, [Line(m.Latte), Line(m.Americano, 1, m.OatMilk)], DiscountType.LoyaltyFree));
        await EnsureAsync(again);
        Assert.Equal(2, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);

        var removed = await client.PutAsJsonAsync($"/api/orders/{order.Id}",
            OrderBody(kee.Id, [Line(m.Latte), Line(m.Americano, 1, m.OatMilk)]));
        await EnsureAsync(removed);

        Assert.DoesNotContain(await LedgerAsync(client, kee.Id), e => e.Reason == LoyaltyReason.Redeemed);
        Assert.Equal(12, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);
    }

    [Fact]
    public async Task AnOrderWithPayments_CannotBeChanged()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee locked");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte)]));
        await EnsureAsync(await PayAsync(client, order.Id, 50m));

        var response = await client.PutAsJsonAsync($"/api/orders/{order.Id}",
            OrderBody(kee.Id, [Line(m.Latte)], DiscountType.Fixed, 10m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(160m, (await GetOrderAsync(client, order.Id)).Total);
    }

    [Fact]
    public async Task Delete_SoftDeletesItemsAddOnsAndPayments_AndVoidsItsLoyalty()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee delete");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte, 2, m.OatMilk)]));
        await EnsureAsync(await PayAsync(client, order.Id, order.Total));
        Assert.Equal(2, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);

        var response = await client.DeleteAsync($"/api/orders/{order.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{order.Id}")).StatusCode);
        Assert.Equal(0, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);

        var stillThere = await _factory.WithDbAsync(async db => new
        {
            Items = await db.OrderItems.IgnoreQueryFilters().Where(i => i.OrderId == order.Id).Select(i => i.DeletedAt).ToListAsync(),
            AddOns = await db.OrderItemAddOns.IgnoreQueryFilters().Where(a => a.OrderItem!.OrderId == order.Id).Select(a => a.DeletedAt).ToListAsync(),
            Payments = await db.Payments.IgnoreQueryFilters().Where(p => p.OrderId == order.Id).Select(p => p.DeletedAt).ToListAsync()
        });
        Assert.All(stillThere.Items, d => Assert.NotNull(d));
        Assert.All(stillThere.AddOns, d => Assert.NotNull(d));
        Assert.All(stillThere.Payments, d => Assert.NotNull(d));
        Assert.NotEmpty(stillThere.AddOns);
    }

    [Fact]
    public async Task PayingForYourself_IsJustPaying_AndAnInactiveCustomerCannotStartATab()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee self payer");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte)], payerId: kee.Id));
        Assert.Null(order.Payer);

        var gone = await CreateCustomerAsync(client, "Kee inactive");
        await client.PatchAsync($"/api/customers/{gone.Id}/deactivate", null);
        var response = await PostOrderAsync(client, OrderBody(gone.Id, [Line(m.Latte)]));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownItemsAndAddOns_Are400()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee unknown");

        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostOrderAsync(client, OrderBody(kee.Id, [Line(Guid.NewGuid())]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte, 1, Guid.NewGuid())]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostOrderAsync(client, OrderBody(kee.Id, []))).StatusCode);
    }

    [Fact]
    public async Task List_FiltersByCustomerDatesAndDerivedStatus_NewestFirst()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee list");
        var older = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano)], orderedOn: new DateOnly(2026, 10, 1)));
        var newer = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano)], orderedOn: new DateOnly(2026, 11, 2)));
        await EnsureAsync(await PayAsync(client, newer.Id, 50m));

        var all = await client.GetFromJsonAsync<PagedResult<OrderSummaryDto>>(
            $"/api/orders?customerId={kee.Id}", KloweeApiFactory.Json);
        Assert.Equal([newer.Id, older.Id], all!.Items.Select(o => o.Id));

        var partial = await client.GetFromJsonAsync<PagedResult<OrderSummaryDto>>(
            $"/api/orders?customerId={kee.Id}&status=Partial", KloweeApiFactory.Json);
        Assert.Equal(newer.Id, Assert.Single(partial!.Items).Id);
        Assert.Equal(50m, partial.Items[0].Paid);

        var october = await client.GetFromJsonAsync<PagedResult<OrderSummaryDto>>(
            $"/api/orders?customerId={kee.Id}&from=2026-10-01&to=2026-10-31", KloweeApiFactory.Json);
        Assert.Equal(older.Id, Assert.Single(october!.Items).Id);

        var paged = await client.GetFromJsonAsync<PagedResult<OrderSummaryDto>>(
            $"/api/orders?customerId={kee.Id}&page=2&pageSize=1", KloweeApiFactory.Json);
        Assert.Equal(2, paged!.TotalCount);
        Assert.Equal(older.Id, Assert.Single(paged.Items).Id);
    }

    [Fact]
    public async Task Orders_RequireAToken() =>
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await (await _factory.CreateAnonymousClientAsync()).GetAsync("/api/orders")).StatusCode);
}
