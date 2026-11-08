using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Entities;
using static Klowee.Api.Tests.SalesFixture;

namespace Klowee.Api.Tests;

public class PaymentTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public PaymentTests(KloweeApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, SalesMenu Menu)> SetupAsync() =>
        (await _factory.CreateOwnerClientAsync(), await SeedMenuAsync(_factory, "payments"));

    [Fact]
    public async Task Status_IsDerived_UnpaidThenPartialThenPaid()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee status");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte)]));
        Assert.Equal(PaymentStatus.Unpaid, order.Status);

        var partial = await (await PayAsync(client, order.Id, 60m)).Content.ReadFromJsonAsync<OrderDetailDto>(KloweeApiFactory.Json);
        Assert.Equal(PaymentStatus.Partial, partial!.Status);
        Assert.Equal(100m, partial.Balance);

        var paid = await (await PayAsync(client, order.Id, 100m, PaymentChannel.Cash)).Content.ReadFromJsonAsync<OrderDetailDto>(KloweeApiFactory.Json);
        Assert.Equal(PaymentStatus.Paid, paid!.Status);
        Assert.Equal(2, paid.Payments.Count);
    }

    [Fact]
    public async Task Overpaying_Is409_AndRecordsNothing()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee overpay");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano)]));

        Assert.Equal(HttpStatusCode.Conflict, (await PayAsync(client, order.Id, 110.01m)).StatusCode);
        Assert.Empty((await GetOrderAsync(client, order.Id)).Payments);

        await EnsureAsync(await PayAsync(client, order.Id, 110m));
        Assert.Equal(HttpStatusCode.Conflict, (await PayAsync(client, order.Id, 1m)).StatusCode);
    }

    [Fact]
    public async Task BecomingPaid_EarnsAStampPerDrink_AndOnlyThen()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee stamps");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte, 2, m.OatMilk), Line(m.Americano)]));

        await EnsureAsync(await PayAsync(client, order.Id, 100m));
        Assert.Equal(0, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);   // partial earns nothing

        await EnsureAsync(await PayAsync(client, order.Id, order.Total - 100m));
        var earned = Assert.Single(await LedgerAsync(client, kee.Id));
        Assert.Equal(LoyaltyReason.Earned, earned.Reason);
        Assert.Equal(3, earned.Delta);                                               // 2 lattes + 1 americano
        Assert.Equal(order.Id, earned.OrderId);
    }

    [Fact]
    public async Task DeletingAPayment_BelowPaid_ReversesTheStamps()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee reverse");
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte, 2)]));
        var paid = await (await PayAsync(client, order.Id, 320m)).Content.ReadFromJsonAsync<OrderDetailDto>(KloweeApiFactory.Json);
        Assert.Equal(2, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);

        var response = await client.DeleteAsync($"/api/orders/{order.Id}/payments/{paid!.Payments.Single().Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);
        Assert.Empty(await LedgerAsync(client, kee.Id));
        Assert.Equal(PaymentStatus.Unpaid, (await GetOrderAsync(client, order.Id)).Status);

        // Paying again earns them again, once.
        await EnsureAsync(await PayAsync(client, order.Id, 320m));
        Assert.Equal(2, Assert.Single(await LedgerAsync(client, kee.Id)).Delta);
    }

    [Fact]
    public async Task ALoyaltyFreeOrder_EarnsForEveryDrinkButTheFreeOne()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee free earns");
        await GiveStampsAsync(client, kee.Id, 10);

        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte, 2), Line(m.Americano)], DiscountType.LoyaltyFree));
        await EnsureAsync(await PayAsync(client, order.Id, order.Total));

        var ledger = await LedgerAsync(client, kee.Id);
        Assert.Equal(2, Assert.Single(ledger, e => e.Reason == LoyaltyReason.Earned).Delta);     // 3 drinks, 1 free
        Assert.Equal(-10, Assert.Single(ledger, e => e.Reason == LoyaltyReason.Redeemed).Delta);
        Assert.Equal(2, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);               // 10 - 10 + 2
    }

    [Fact]
    public async Task AFullyFreeOrder_IsPaidWithoutPayments()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee all free");
        await GiveStampsAsync(client, kee.Id, 10);

        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano)], DiscountType.LoyaltyFree));

        Assert.Equal(0m, order.Total);
        Assert.Equal(PaymentStatus.Paid, order.Status);
        Assert.Equal(0, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);  // redeemed; the free drink earns nothing
    }

    [Fact]
    public async Task Settle_AllocatesOldestFirst_AcrossThreeOrders_IncludingAPartial()
    {
        var (client, m) = await SetupAsync();
        var prince = await CreateCustomerAsync(client, "Prince settle");
        var oldest = await CreateOrderAsync(client, OrderBody(prince.Id, [Line(m.Latte)], orderedOn: new DateOnly(2026, 10, 1)));     // 160
        var middle = await CreateOrderAsync(client, OrderBody(prince.Id, [Line(m.Americano)], orderedOn: new DateOnly(2026, 10, 8)));  // 110
        var newest = await CreateOrderAsync(client, OrderBody(prince.Id, [Line(m.Matcha)], orderedOn: new DateOnly(2026, 10, 15)));    // 150
        await EnsureAsync(await PayAsync(client, oldest.Id, 100m));   // oldest owes 60 now

        var response = await client.PostAsJsonAsync($"/api/customers/{prince.Id}/settle", PaymentBody(250m, PaymentChannel.BPI, reference: "BPI-7781"));
        await EnsureAsync(response);
        var payments = (await response.Content.ReadFromJsonAsync<List<PaymentDto>>(KloweeApiFactory.Json))!;

        // 60 finishes the oldest, 110 the middle, the last 80 goes to the newest.
        Assert.Equal([oldest.Id, middle.Id, newest.Id], payments.Select(p => p.OrderId));
        Assert.Equal([60m, 110m, 80m], payments.Select(p => p.Amount));
        Assert.All(payments, p => Assert.Equal(PaymentChannel.BPI, p.Channel));
        Assert.All(payments, p => Assert.Equal("BPI-7781", p.Reference));

        Assert.Equal(PaymentStatus.Paid, (await GetOrderAsync(client, oldest.Id)).Status);
        Assert.Equal(PaymentStatus.Paid, (await GetOrderAsync(client, middle.Id)).Status);
        Assert.Equal(PaymentStatus.Partial, (await GetOrderAsync(client, newest.Id)).Status);
        Assert.Equal(70m, (await GetCustomerAsync(client, prince.Id)).OutstandingBalance);
        Assert.Equal(2, (await GetCustomerAsync(client, prince.Id)).LoyaltyBalance);   // two orders became Paid
    }

    [Fact]
    public async Task Settle_MoreThanTheTabOwes_Is409_AndRecordsNothing()
    {
        var (client, m) = await SetupAsync();
        var prince = await CreateCustomerAsync(client, "Prince oversettle");
        var order = await CreateOrderAsync(client, OrderBody(prince.Id, [Line(m.Americano)]));

        var response = await client.PostAsJsonAsync($"/api/customers/{prince.Id}/settle", PaymentBody(110.01m));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty((await GetOrderAsync(client, order.Id)).Payments);
    }

    [Fact]
    public async Task ThePayersTab_HoldsTheOrdersTheyCover_AndSettleChargesThem()
    {
        var (client, m) = await SetupAsync();
        var prince = await CreateCustomerAsync(client, "Prince covered");
        var kee = await CreateCustomerAsync(client, "Kee covers");

        // "Prince c/o Kee": Prince drinks, Kee pays.
        var order = await CreateOrderAsync(client, OrderBody(prince.Id, [Line(m.Latte)], payerId: kee.Id));
        Assert.Equal("Kee covers", order.Payer!.Name);

        Assert.Equal(0m, (await GetCustomerAsync(client, prince.Id)).OutstandingBalance);
        Assert.Equal(160m, (await GetCustomerAsync(client, kee.Id)).OutstandingBalance);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync($"/api/customers/{prince.Id}/settle", PaymentBody(10m))).StatusCode);

        await EnsureAsync(await client.PostAsJsonAsync($"/api/customers/{kee.Id}/settle", PaymentBody(160m)));

        // The stamp goes to the drinker, not the payer.
        Assert.Equal(1, (await GetCustomerAsync(client, prince.Id)).LoyaltyBalance);
        Assert.Equal(0, (await GetCustomerAsync(client, kee.Id)).LoyaltyBalance);
    }

    [Fact]
    public async Task APaymentOnAMissingOrderOrOfAnotherOrder_Is404()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCustomerAsync(client, "Kee 404");
        var first = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte)]));
        var second = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Latte)]));
        var paid = await (await PayAsync(client, first.Id, 10m)).Content.ReadFromJsonAsync<OrderDetailDto>(KloweeApiFactory.Json);

        Assert.Equal(HttpStatusCode.NotFound, (await PayAsync(client, Guid.NewGuid(), 10m)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.DeleteAsync($"/api/orders/{second.Id}/payments/{paid!.Payments.Single().Id}")).StatusCode);
    }
}
