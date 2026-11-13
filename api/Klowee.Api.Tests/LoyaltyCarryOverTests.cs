using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.EntityFrameworkCore;
using static Klowee.Api.Tests.SalesFixture;

namespace Klowee.Api.Tests;

/// <summary>
/// The one-time carry-over of the old single card onto The Coffee Club, on a
/// fixture of old balances 0, 4, 6, 11 and 23. Its own database: it is a
/// one-time step, and the marker row it writes is global.
/// </summary>
public class LoyaltyCarryOverTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public LoyaltyCarryOverTests(KloweeApiFactory factory) => _factory = factory;

    private sealed record Fixture(Dictionary<int, Guid> Customers, Guid PaidOrder, Guid NoEntries);

    /// <summary>
    /// The database as the LoyaltyClubs migration leaves it: the old entries,
    /// all on The Coffee Club (the column's backfill), the categories unmapped.
    /// Balances: 0 (+10 earned, −10 redeemed), 4, 6, 11 (+12, −1), 23.
    /// </summary>
    private async Task<Fixture> ArrangeAsync()
    {
        var m = await SeedMenuAsync(_factory, "carry");
        return await _factory.WithDbAsync(async db =>
        {
            // Undo the fixture's mapping, and add the names handover 16 maps.
            foreach (var c in await db.MenuCategories.ToListAsync())
            {
                c.LoyaltyClubId = null;
            }

            db.MenuCategories.AddRange(
                new MenuCategory { Name = "coffee", SortOrder = 1 },                      // matched ignoring case
                new MenuCategory { Name = "Signature Coffee", SortOrder = 2 },
                new MenuCategory { Name = "Houseblend Matcha", SortOrder = 3 },
                new MenuCategory { Name = "Refreshers", SortOrder = 4 });                // stays unmapped
            var dirty = new MenuItem { Name = "Dirty Matcha", Category = await db.MenuCategories.FirstAsync() };
            db.MenuItems.Add(dirty);

            var customers = new Dictionary<int, Guid>();
            foreach (var balance in new[] { 0, 4, 6, 11, 23 })
            {
                var customer = new Customer { Name = $"Old card {balance}" };
                db.Customers.Add(customer);
                customers[balance] = customer.Id = Guid.NewGuid();
            }

            Entry(customers[0], 10, LoyaltyReason.Earned);
            Entry(customers[0], -10, LoyaltyReason.Redeemed);
            Entry(customers[4], 4, LoyaltyReason.Earned);
            Entry(customers[6], 3, LoyaltyReason.Earned);
            Entry(customers[6], 3, LoyaltyReason.Adjustment);
            Entry(customers[11], 12, LoyaltyReason.Earned);
            Entry(customers[11], -1, LoyaltyReason.Adjustment);
            Entry(customers[23], 23, LoyaltyReason.Earned);

            // One of the 4's stamps came from a paid order, the way the import left them.
            var order = new Order
            {
                CustomerId = customers[4], OrderedOn = new DateOnly(2026, 10, 1), Subtotal = 160m, Total = 160m,
                Items = { new OrderItem { MenuVersionItemId = m.Latte, Quantity = 1, UnitPrice = 160m, LineTotal = 160m } },
                Payments = { new Payment { Amount = 160m, Channel = PaymentChannel.Cash, PaidOn = new DateOnly(2026, 10, 1) } }
            };
            db.Orders.Add(order);
            db.LoyaltyEntries.Add(new LoyaltyEntry
            {
                CustomerId = customers[4], ClubId = LoyaltyClub.CoffeeClubId, Order = order, Delta = 1, Reason = LoyaltyReason.Earned
            });
            await db.SaveChangesAsync();

            // The 4 now sums to 5 with the order's stamp; take one back so it still reads 4.
            db.LoyaltyEntries.Add(new LoyaltyEntry
            {
                CustomerId = customers[4], ClubId = LoyaltyClub.CoffeeClubId, Delta = -1, Reason = LoyaltyReason.Adjustment
            });

            var noEntries = new Customer { Name = "Never stamped" };
            db.Customers.Add(noEntries);
            await db.SaveChangesAsync();
            return new Fixture(customers, order.Id, noEntries.Id);

            void Entry(Guid customer, int delta, LoyaltyReason reason) =>
                db.LoyaltyEntries.Add(new LoyaltyEntry { CustomerId = customer, ClubId = LoyaltyClub.CoffeeClubId, Delta = delta, Reason = reason });
        });
    }

    [Fact]
    public async Task CarriesEachBalance_CappedAt11_PreUsesPassedRewards_MapsCategories_AndRunsOnce()
    {
        var fixture = await ArrangeAsync();
        var client = await _factory.CreateOwnerClientAsync();
        var (orders, payments) = await _factory.WithDbAsync(async db =>
            (await db.Orders.IgnoreQueryFilters().CountAsync(), await db.Payments.IgnoreQueryFilters().CountAsync()));

        var result = await _factory.WithDbAsync(db => new LoyaltyCarryOver(db).RunAsync());

        // Old balance → carried, and which Coffee Club rewards count as already used on card 1.
        Assert.Equal(
            [(0, 0, 0), (4, 4, 0), (6, 6, 1), (11, 11, 2), (23, 11, 2)],
            result.Customers.OrderBy(c => c.OldBalance).Select(c => (c.OldBalance, c.Carried, c.RewardsMarkedUsed)));

        foreach (var (balance, expected) in new Dictionary<int, (int, string, string, string)>
                 {
                     [0] = (0, "locked", "locked", "locked"),
                     [4] = (4, "available", "locked", "locked"),       // the 5th drink gets 50% off
                     [6] = (6, "used", "locked", "locked"),
                     [11] = (11, "used", "used", "available"),         // the free drink is waiting
                     [23] = (11, "used", "used", "available"),
                 })
        {
            var card = await CardAsync(client, fixture.Customers[balance]);
            var states = card.Rewards.Where(r => r.Cycle == 0).OrderBy(r => r.Slot).Select(r => r.State).ToList();
            Assert.Equal(expected, (card.Position, states[0], states[1], states[2]));
        }

        // The old entries are voided history; one adjustment per customer with stamps.
        var ledger = await LedgerAsync(client, fixture.Customers[23]);
        var carried = Assert.Single(ledger);
        Assert.Equal((LoyaltyReason.Adjustment, 11, LoyaltyCarryOver.AdjustmentNote), (carried.Reason, carried.Delta, carried.Notes));
        Assert.Empty(await LedgerAsync(client, fixture.Customers[0]));
        Assert.Equal(3, await _factory.WithDbAsync(db => db.LoyaltyEntries.IgnoreQueryFilters()
            .CountAsync(e => e.CustomerId == fixture.Customers[4] && e.CarriedOverAt != null)));

        // Mapping, with the names not found reported.
        Assert.Contains("coffee → The Coffee Club", result.CategoriesMapped);
        Assert.Contains("Dirty Matcha (item) → The Matcha Club", result.CategoriesMapped);
        Assert.Contains("category \"Okumidori Matcha\"", result.NamesNotFound);
        var refreshers = await _factory.WithDbAsync(db => db.MenuCategories.SingleAsync(c => c.Name == "Refreshers"));
        Assert.Null(refreshers.LoyaltyClubId);

        // Orders and payments untouched.
        Assert.Equal((orders, payments), await _factory.WithDbAsync(async db =>
            (await db.Orders.IgnoreQueryFilters().CountAsync(), await db.Payments.IgnoreQueryFilters().CountAsync())));

        // Twice is once: the marker stops it, and without the marker every customer is already done.
        Assert.True((await _factory.WithDbAsync(db => new LoyaltyCarryOver(db).RunAsync())).AlreadyApplied);
        await _factory.WithDbAsync(async db =>
        {
            db.DataMigrations.RemoveRange(db.DataMigrations);
            return await db.SaveChangesAsync();
        });
        var again = await _factory.WithDbAsync(db => new LoyaltyCarryOver(db).RunAsync());
        Assert.Empty(again.Customers);
        Assert.Empty(again.CategoriesMapped);
        Assert.Equal(11, await StampsAsync(client, fixture.Customers[23]));
        Assert.Equal(2, (await _factory.WithDbAsync(db => db.LoyaltyRedemptions.Where(r => r.CustomerId == fixture.Customers[11]).ToListAsync())).Count);

        // A carried-over paid order never stamps again, even when its payment is
        // redone and its drink now belongs to a club.
        await _factory.WithDbAsync(async db =>
        {
            var latte = await db.MenuCategories.SingleAsync(c => c.Name == "Coffee carry");
            latte.LoyaltyClubId = LoyaltyClub.CoffeeClubId;
            return await db.SaveChangesAsync();
        });
        var payment = await _factory.WithDbAsync(db => db.Payments.SingleAsync(p => p.OrderId == fixture.PaidOrder));
        await EnsureAsync(await client.DeleteAsync($"/api/orders/{fixture.PaidOrder}/payments/{payment.Id}"));
        await EnsureAsync(await PayAsync(client, fixture.PaidOrder, 160m));
        Assert.Equal(4, await StampsAsync(client, fixture.Customers[4]));
    }
}
