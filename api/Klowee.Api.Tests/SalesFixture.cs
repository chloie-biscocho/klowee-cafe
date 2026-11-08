using System.Net.Http.Json;
using Klowee.Api.Contracts.Customers;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Tests;

/// <summary>Ids of the small priced menu each sales test class sells from.</summary>
public record SalesMenu(
    Guid Latte,        // 160, current office menu
    Guid Americano,    // 110, current office menu
    Guid Matcha,       // 150, current office menu
    Guid OldPopUpLatte,// 140, an old pop-up version: allowed, but warned about
    Guid OatMilk,      // add-on, 30
    Guid ExtraShot,    // add-on, 25
    Guid LatteVersionItemPriceRow);

/// <summary>
/// Shared setup for the phase 2 tests: a menu written straight to the test
/// database, and thin HTTP helpers. Each test class has its own factory, so
/// its own database, and names carry a per-class suffix anyway.
/// </summary>
public static class SalesFixture
{
    public static readonly DateOnly Today = new(2026, 11, 8);

    public static Task<SalesMenu> SeedMenuAsync(KloweeApiFactory factory, string suffix)
    {
        factory.Clock.Today = Today;

        return factory.WithDbAsync(async db =>
        {
            // xUnit makes a new test-class instance per test but shares the
            // factory (and its database) across the class: seed once, then reuse.
            var existing = await db.MenuVersions
                .Where(v => v.Name == $"Office menu {suffix}" || v.Name == $"Old pop-up {suffix}")
                .SelectMany(v => v.Items)
                .Select(vi => new { vi.Id, vi.Price, Version = vi.MenuVersion!.Name, Item = vi.MenuItem!.Name })
                .ToListAsync();
            if (existing.Count > 0)
            {
                Guid Row(string version, string item) =>
                    existing.Single(e => e.Version == $"{version} {suffix}" && e.Item == $"{item} {suffix}").Id;
                var addOns = await db.AddOns.Where(a => a.Name.EndsWith(suffix)).ToDictionaryAsync(a => a.Name, a => a.Id);
                return new SalesMenu(
                    Row("Office menu", "Latte"), Row("Office menu", "Americano"), Row("Office menu", "Matcha"),
                    Row("Old pop-up", "Latte"), addOns[$"Oat milk {suffix}"], addOns[$"Extra shot {suffix}"],
                    Row("Office menu", "Latte"));
            }

            var category = new MenuCategory { Name = $"Coffee {suffix}", SortOrder = 1 };
            var latte = new MenuItem { Name = $"Latte {suffix}", Category = category };
            var americano = new MenuItem { Name = $"Americano {suffix}", Category = category };
            var matcha = new MenuItem { Name = $"Matcha {suffix}", Category = category };

            var office = new MenuVersion
            {
                Name = $"Office menu {suffix}", Context = MenuContext.Office,
                EffectiveFrom = new DateOnly(2026, 1, 1), IsPublished = true
            };
            var popUp = new MenuVersion
            {
                Name = $"Old pop-up {suffix}", Context = MenuContext.PopUp,
                EffectiveFrom = new DateOnly(2025, 6, 1), IsPublished = true
            };

            var latteRow = new MenuVersionItem { MenuVersion = office, MenuItem = latte, Price = 160m };
            var americanoRow = new MenuVersionItem { MenuVersion = office, MenuItem = americano, Price = 110m };
            var matchaRow = new MenuVersionItem { MenuVersion = office, MenuItem = matcha, Price = 150m };
            var oldLatteRow = new MenuVersionItem { MenuVersion = popUp, MenuItem = latte, Price = 140m };

            var oat = new AddOn { Name = $"Oat milk {suffix}", Price = 30m };
            var shot = new AddOn { Name = $"Extra shot {suffix}", Price = 25m };

            db.AddRange(category, latte, americano, matcha, office, popUp, latteRow, americanoRow, matchaRow, oldLatteRow, oat, shot);
            await db.SaveChangesAsync();

            return new SalesMenu(latteRow.Id, americanoRow.Id, matchaRow.Id, oldLatteRow.Id, oat.Id, shot.Id, latteRow.Id);
        });
    }

    public static async Task<CustomerDetailDto> CreateCustomerAsync(HttpClient client, string name, string? nickname = null)
    {
        var response = await client.PostAsJsonAsync("/api/customers", new { name, nickname });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CustomerDetailDto>(KloweeApiFactory.Json))!;
    }

    public static object Line(Guid menuVersionItemId, int quantity = 1, params Guid[] addOnIds) =>
        new { menuVersionItemId, quantity, addOnIds };

    public static object OrderBody(
        Guid customerId,
        object[] items,
        DiscountType discountType = DiscountType.None,
        decimal discountValue = 0m,
        Guid? payerId = null,
        DateOnly? orderedOn = null,
        string? notes = null) =>
        new
        {
            customerId,
            payerId,
            orderedOn = orderedOn ?? Today,
            notes,
            discountType = discountType.ToString(),
            discountValue,
            items
        };

    public static async Task<HttpResponseMessage> PostOrderAsync(HttpClient client, object body) =>
        await client.PostAsJsonAsync("/api/orders", body);

    public static async Task<OrderDetailDto> CreateOrderAsync(HttpClient client, object body)
    {
        var response = await PostOrderAsync(client, body);
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<OrderDetailDto>(KloweeApiFactory.Json))!;
    }

    public static object PaymentBody(decimal amount, PaymentChannel channel = PaymentChannel.GCash, DateOnly? paidOn = null, string? reference = null) =>
        new { amount, channel = channel.ToString(), paidOn = paidOn ?? Today, reference };

    public static async Task<HttpResponseMessage> PayAsync(HttpClient client, Guid orderId, decimal amount,
        PaymentChannel channel = PaymentChannel.GCash, DateOnly? paidOn = null) =>
        await client.PostAsJsonAsync($"/api/orders/{orderId}/payments", PaymentBody(amount, channel, paidOn));

    public static async Task<OrderDetailDto> GetOrderAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<OrderDetailDto>($"/api/orders/{id}", KloweeApiFactory.Json))!;

    public static async Task<CustomerDetailDto> GetCustomerAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<CustomerDetailDto>($"/api/customers/{id}", KloweeApiFactory.Json))!;

    public static async Task<List<LoyaltyEntryDto>> LedgerAsync(HttpClient client, Guid customerId) =>
        (await client.GetFromJsonAsync<List<LoyaltyEntryDto>>($"/api/customers/{customerId}/loyalty", KloweeApiFactory.Json))!;

    /// <summary>Gives a customer stamps directly, as the owners would with an adjustment.</summary>
    public static async Task GiveStampsAsync(HttpClient client, Guid customerId, int stamps)
    {
        var response = await client.PostAsJsonAsync($"/api/customers/{customerId}/loyalty/adjust", new { delta = stamps, notes = "test" });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>EnsureSuccessStatusCode, but with the ProblemDetails body in the failure message.</summary>
    public static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new Xunit.Sdk.XunitException(
                $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }
    }
}
