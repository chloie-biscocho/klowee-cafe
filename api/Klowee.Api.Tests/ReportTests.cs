using System.Net.Http.Json;
using Klowee.Api.Contracts.Reports;
using Klowee.Api.Entities;
using static Klowee.Api.Tests.SalesFixture;

namespace Klowee.Api.Tests;

/// <summary>
/// Report sums against a fixture worked out by hand. This class has its own
/// factory, so its database holds exactly these orders:
///
///   O1  A  2026-10-05  2 × Latte                  320   paid 320 on 10-06 (GCash)
///   O5  A  2026-10-20  Latte, 10% off             144   paid 144 on 11-01 (Maya)    Oct revenue, Nov collection
///   O2  A  2026-11-02  Americano                  110   paid  50 on 11-03 (Cash)    owes 60
///   O3  B  2026-11-04  Matcha + Latte/oat, −40    300   unpaid                      owes 300
///   O4  C  2026-11-06  3 × Americano, B pays      330   paid 330 on 11-07 (BPI)
///   O6  A  2026-11-07  Latte                      160   deleted: counts nowhere
/// </summary>
public class ReportTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public ReportTests(KloweeApiFactory factory) => _factory = factory;

    private async Task<HttpClient> SeedAsync()
    {
        var client = await _factory.CreateOwnerClientAsync();
        var m = await SeedMenuAsync(_factory, "reports");

        var existing = await client.GetFromJsonAsync<List<Contracts.Customers.CustomerSummaryDto>>(
            "/api/customers?q=Report", KloweeApiFactory.Json);
        if (existing!.Count > 0)
        {
            return client;
        }

        var a = await CreateCustomerAsync(client, "Report A");
        var b = await CreateCustomerAsync(client, "Report B");
        var c = await CreateCustomerAsync(client, "Report C");

        var o1 = await CreateOrderAsync(client, OrderBody(a.Id, [Line(m.Latte, 2)], orderedOn: new DateOnly(2026, 10, 5)));
        await EnsureAsync(await PayAsync(client, o1.Id, 320m, PaymentChannel.GCash, new DateOnly(2026, 10, 6)));

        var o5 = await CreateOrderAsync(client, OrderBody(a.Id, [Line(m.Latte)], DiscountType.Percent, 10m, orderedOn: new DateOnly(2026, 10, 20)));
        await EnsureAsync(await PayAsync(client, o5.Id, 144m, PaymentChannel.Maya, new DateOnly(2026, 11, 1)));

        var o2 = await CreateOrderAsync(client, OrderBody(a.Id, [Line(m.Americano)], orderedOn: new DateOnly(2026, 11, 2)));
        await EnsureAsync(await PayAsync(client, o2.Id, 50m, PaymentChannel.Cash, new DateOnly(2026, 11, 3)));

        await CreateOrderAsync(client, OrderBody(b.Id, [Line(m.Matcha), Line(m.Latte, 1, m.OatMilk)], DiscountType.Fixed, 40m,
            orderedOn: new DateOnly(2026, 11, 4)));

        var o4 = await CreateOrderAsync(client, OrderBody(c.Id, [Line(m.Americano, 3)], payerId: b.Id, orderedOn: new DateOnly(2026, 11, 6)));
        await EnsureAsync(await PayAsync(client, o4.Id, 330m, PaymentChannel.BPI, new DateOnly(2026, 11, 7)));

        var o6 = await CreateOrderAsync(client, OrderBody(a.Id, [Line(m.Latte)], orderedOn: new DateOnly(2026, 11, 7)));
        (await client.DeleteAsync($"/api/orders/{o6.Id}")).EnsureSuccessStatusCode();

        return client;
    }

    [Fact]
    public async Task Summary_ForNovember()
    {
        var client = await SeedAsync();

        var s = (await client.GetFromJsonAsync<SummaryReportDto>(
            "/api/reports/summary?from=2026-11-01&to=2026-11-30", KloweeApiFactory.Json))!;

        Assert.Equal(110m + 300m + 330m, s.Revenue);              // O2, O3, O4
        Assert.Equal(50m + 330m + 144m, s.Collected);             // by paid_on, O5's included
        Assert.Equal(60m + 300m, s.Outstanding);                  // all time
        Assert.Equal(3, s.OrderCount);
        Assert.Equal(1 + 2 + 3, s.DrinkCount);
        Assert.Equal(
            [(PaymentChannel.BPI, 330m, 1), (PaymentChannel.Maya, 144m, 1), (PaymentChannel.Cash, 50m, 1)],
            s.ByChannel.Select(c => (c.Channel, c.Amount, c.Count)));
        Assert.Equal(
            [("Americano reports", 4), ("Latte reports", 1), ("Matcha reports", 1)],
            s.TopItems.Select(t => (t.Name, t.Quantity)));
        Assert.Equal(2, s.NewCustomers);                          // B and C first ordered in November
    }

    [Fact]
    public async Task Summary_DefaultsToTheCurrentMonthSoFar()
    {
        var client = await SeedAsync();

        var s = (await client.GetFromJsonAsync<SummaryReportDto>("/api/reports/summary", KloweeApiFactory.Json))!;

        Assert.Equal(new DateOnly(2026, 11, 1), s.From);
        Assert.Equal(Today, s.To);
        Assert.Equal(740m, s.Revenue);
    }

    [Fact]
    public async Task Outstanding_PerTabOwner_LargestFirst()
    {
        var client = await SeedAsync();

        var rows = (await client.GetFromJsonAsync<List<OutstandingRowDto>>("/api/reports/outstanding", KloweeApiFactory.Json))!;

        Assert.Equal(
            [("Report B", 300m, new DateOnly(2026, 11, 4), 1), ("Report A", 60m, new DateOnly(2026, 11, 2), 1)],
            rows.Select(r => (r.Name, r.Balance, r.OldestUnpaidOn, r.OrderCount)));
    }

    [Fact]
    public async Task Monthly_TwelveRows_RevenueByOrderDate_CollectedByPaymentDate()
    {
        var client = await SeedAsync();

        var rows = (await client.GetFromJsonAsync<List<MonthlyRowDto>>("/api/reports/monthly?year=2026", KloweeApiFactory.Json))!;

        Assert.Equal(Enumerable.Range(1, 12), rows.Select(r => r.Month));
        Assert.All(rows, r => Assert.Equal(2026, r.Year));
        Assert.Equal((10, 464m, 320m, 2, 3), (rows[9].Month, rows[9].Revenue, rows[9].Collected, rows[9].Orders, rows[9].Drinks));
        Assert.Equal((11, 740m, 524m, 3, 6), (rows[10].Month, rows[10].Revenue, rows[10].Collected, rows[10].Orders, rows[10].Drinks));
        Assert.All(rows.Where(r => r.Month < 10 || r.Month == 12), r => Assert.Equal(0m, r.Revenue + r.Collected));
    }

    [Fact]
    public async Task MonthlyRange_OneRowPerMonth_AcrossYears_EmptyMonthsZero()
    {
        var client = await SeedAsync();

        var rows = (await client.GetFromJsonAsync<List<MonthlyRowDto>>("/api/reports/monthly?from=2026-09&to=2027-02", KloweeApiFactory.Json))!;

        Assert.Equal(
            [(2026, 9), (2026, 10), (2026, 11), (2026, 12), (2027, 1), (2027, 2)],
            rows.Select(r => (r.Year, r.Month)));
        Assert.Equal((464m, 320m, 2, 3), (rows[1].Revenue, rows[1].Collected, rows[1].Orders, rows[1].Drinks));
        Assert.Equal((740m, 524m, 3, 6), (rows[2].Revenue, rows[2].Collected, rows[2].Orders, rows[2].Drinks));
        Assert.All(rows.Where((_, i) => i is not 1 and not 2), r => Assert.Equal(0m, r.Revenue + r.Collected));
    }

    [Fact]
    public async Task MonthlyRange_WithoutFrom_StartsAtTheFirstMonthWithAnOrder()
    {
        var client = await SeedAsync();

        var rows = (await client.GetFromJsonAsync<List<MonthlyRowDto>>("/api/reports/monthly?to=2026-12", KloweeApiFactory.Json))!;

        Assert.Equal([(2026, 10), (2026, 11), (2026, 12)], rows.Select(r => (r.Year, r.Month)));
    }

    [Theory]
    [InlineData("from=2026-11&to=2026-10")]
    [InlineData("year=2026&from=2026-01&to=2026-02")]
    [InlineData("from=2026-01")]
    [InlineData("from=2026-1&to=2026-02")]
    [InlineData("from=2000-01&to=2026-12")]
    public async Task MonthlyRange_BadInput_400(string query)
    {
        var client = await SeedAsync();

        var response = await client.GetAsync($"/api/reports/monthly?{query}");

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
