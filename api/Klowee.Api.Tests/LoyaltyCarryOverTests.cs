using Klowee.Api.Entities;
using Klowee.Api.Services;
using Microsoft.EntityFrameworkCore;
using static Klowee.Api.Tests.SalesFixture;

namespace Klowee.Api.Tests;

/// <summary>
/// The one-time carry-over of the paper Coffee Club cards from the
/// owner-reviewed list (handover 16a). Its own database: the marker row is
/// global, so each test starts from an undone carry-over.
/// </summary>
public class LoyaltyCarryOverTests : IClassFixture<KloweeApiFactory>
{
    private readonly KloweeApiFactory _factory;

    public LoyaltyCarryOverTests(KloweeApiFactory factory) => _factory = factory;

    private sealed record Fixture(Dictionary<string, Guid> Customers, Guid PaidOrder, Guid UnpaidOrder);

    /// <summary>
    /// The database as the migrations leave it: customers with old single-card
    /// entries (all on The Coffee Club, the column's backfill), one paid order
    /// whose stamp the old card counted, and one unpaid order.
    /// </summary>
    private async Task<Fixture> ArrangeAsync(string suffix)
    {
        var m = await SeedMenuAsync(_factory, "carry");
        await _factory.WithDbAsync(db => new LoyaltyCarryOver(db).UndoAsync());
        return await _factory.WithDbAsync(async db =>
        {
            var customers = new Dictionary<string, Guid>();
            foreach (var name in new[] { "Card one", "Card two", "Card four", "No card", "Card zero" })
            {
                var customer = new Customer { Name = $"{name} {suffix}" };
                db.Customers.Add(customer);
                customers[name] = customer.Id = Guid.NewGuid();
                db.LoyaltyEntries.Add(new LoyaltyEntry
                {
                    CustomerId = customer.Id, ClubId = LoyaltyClub.CoffeeClubId, Delta = 7, Reason = LoyaltyReason.Earned
                });
            }

            Order NewOrder(Guid customer, bool paid) => new()
            {
                CustomerId = customer, OrderedOn = new DateOnly(2026, 10, 1), Subtotal = 160m, Total = 160m,
                Items = { new OrderItem { MenuVersionItemId = m.Latte, Quantity = 1, UnitPrice = 160m, LineTotal = 160m } },
                Payments = paid ? [new Payment { Amount = 160m, Channel = PaymentChannel.Cash, PaidOn = new DateOnly(2026, 10, 1) }] : []
            };

            var paidOrder = NewOrder(customers["Card one"], paid: true);
            var unpaidOrder = NewOrder(customers["Card one"], paid: false);
            db.Orders.AddRange(paidOrder, unpaidOrder);
            db.LoyaltyEntries.Add(new LoyaltyEntry
            {
                CustomerId = customers["Card one"], ClubId = LoyaltyClub.CoffeeClubId, Order = paidOrder, Delta = 1, Reason = LoyaltyReason.Earned
            });
            await db.SaveChangesAsync();
            return new Fixture(customers, paidOrder.Id, unpaidOrder.Id);
        });
    }

    /// <summary>The list as the owner saves it from a spreadsheet: Windows line endings, reformatted dates, a blank row.</summary>
    private static string WriteList(string suffix, params string[] rows)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cardholders-{Guid.NewGuid():N}.csv");
        var lines = new[] { "customer,has_card,card_number,position,estimate,last_reward,last_reward_on,coffees_paid_since,evidence,flag" }
            .Concat(rows.Select(r => r.Replace("{s}", suffix)))
            .Append(",, ,,,,,,,");
        File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n");
        return path;
    }

    private static string[] Rows =>
    [
        "Card one {s},yes,1,,6,50% off (slot 5),10/21/2025,1,\"notes \"\"50% loyalty\"\"\",",
        "Card two {s},yes,2,3,9,,,,,",                        // the owner's 3 beats the estimate
        "card four {s},yes,4,7,2,,,,,",                       // names match ignoring case
        "No card {s},no,0,,5,,,,,",
        "Card zero {s},yes,0,,,,,0,,no reward slot",          // 0 means "not set": card 1, position 0
        "Ghost {s},yes,1,,4,,,,,",                            // no such customer: reported, skipped
    ];

    private Task<List<LoyaltyRedemption>> UsedAsync(Guid customer) =>
        _factory.WithDbAsync(db => db.LoyaltyRedemptions.Where(r => r.CustomerId == customer).ToListAsync());

    [Fact]
    public async Task CarriesTheReviewedList_CardsOneTwoAndFour_AndNoOneElse()
    {
        var f = await ArrangeAsync("a");
        var client = await _factory.CreateOwnerClientAsync();
        var (orders, payments) = await _factory.WithDbAsync(async db =>
            (await db.Orders.IgnoreQueryFilters().CountAsync(), await db.Payments.IgnoreQueryFilters().CountAsync()));

        var result = await _factory.WithDbAsync(db => new LoyaltyCarryOver(db, WriteList("a", Rows)).RunAsync());

        Assert.Null(result.Skipped);
        Assert.Equal(
            [("Card four a", 4, 7, 43, 10), ("Card one a", 1, 6, 6, 1), ("Card two a", 2, 3, 15, 3), ("Card zero a", 1, 0, 0, 0)],
            result.Cardholders.OrderBy(c => c.Name).Select(c => (c.Name, c.Card, c.Position, c.Stamps, c.RewardsMarkedUsed)));
        Assert.Contains(result.NamesNotFound, n => n.Contains("Ghost a") && n.Contains("no match"));

        // Card 1: 6 of 12, the 50% passed.
        var one = await CardAsync(client, f.Customers["Card one"]);
        Assert.Equal(("member", 0, 6), (one.Membership, one.Cycle, one.Position));
        Assert.Equal(["used", "locked", "locked"], one.Rewards.Select(r => r.State));

        // Card 2 at 3: 15 stamps; card 1's rewards all used (so not listed), this card's all ahead.
        var two = await CardAsync(client, f.Customers["Card two"]);
        Assert.Equal((1, 3), (two.Cycle, two.Position));
        Assert.Equal([(1, "locked"), (1, "locked"), (1, "locked")], two.Rewards.Select(r => (r.Cycle, r.State)));
        Assert.Equal(3, (await UsedAsync(f.Customers["Card two"])).Count(r => r.Cycle == 0));

        // Card 4 at 7: 43 stamps; three full cards of rewards used, and this card's 50%.
        // Slot 7 is filled, so the extra shot (slot 8) is ready for the next drink.
        var four = await CardAsync(client, f.Customers["Card four"]);
        Assert.Equal((3, 7), (four.Cycle, four.Position));
        Assert.Equal(["used", "available", "locked"], four.Rewards.Select(r => r.State));
        Assert.Equal([3, 3, 3, 1], (await UsedAsync(f.Customers["Card four"])).GroupBy(r => r.Cycle).OrderBy(g => g.Key).Select(g => g.Count()));

        // has_card = no: no card, nothing written, the old entries voided like everyone's.
        var none = await CardAsync(client, f.Customers["No card"]);
        Assert.Equal(("none", 0), (none.Membership, none.Position));
        Assert.Empty(await LedgerAsync(client, f.Customers["No card"]));

        // Card zero: a card from the beginning, nothing to carry.
        var zero = await CardAsync(client, f.Customers["Card zero"]);
        Assert.Equal(("member", 0, 0), (zero.Membership, zero.Cycle, zero.Position));

        // Orders and payments untouched.
        Assert.Equal((orders, payments), await _factory.WithDbAsync(async db =>
            (await db.Orders.IgnoreQueryFilters().CountAsync(), await db.Payments.IgnoreQueryFilters().CountAsync())));

        // The cardholder's unpaid old order stamps when paid; the paid one the old card counted never does again.
        await EnsureAsync(await PayAsync(client, f.UnpaidOrder, 160m));
        Assert.Equal(7, await StampsAsync(client, f.Customers["Card one"]));
        var payment = await _factory.WithDbAsync(db => db.Payments.SingleAsync(p => p.OrderId == f.PaidOrder));
        await EnsureAsync(await client.DeleteAsync($"/api/orders/{f.PaidOrder}/payments/{payment.Id}"));
        await EnsureAsync(await PayAsync(client, f.PaidOrder, 160m));
        Assert.Equal(7, await StampsAsync(client, f.Customers["Card one"]));
    }

    [Fact]
    public async Task RunsOnce_AndTwiceIsOnceEvenWithoutTheMarker()
    {
        var f = await ArrangeAsync("b");
        var list = WriteList("b", Rows);
        var client = await _factory.CreateOwnerClientAsync();
        await _factory.WithDbAsync(db => new LoyaltyCarryOver(db, list).RunAsync());
        await EnsureAsync(await PayAsync(client, f.UnpaidOrder, 160m));          // a real stamp after the carry-over

        Assert.True((await _factory.WithDbAsync(db => new LoyaltyCarryOver(db, list).RunAsync())).AlreadyApplied);

        await _factory.WithDbAsync(async db =>
        {
            db.DataMigrations.RemoveRange(db.DataMigrations);
            return await db.SaveChangesAsync();
        });
        var again = await _factory.WithDbAsync(db => new LoyaltyCarryOver(db, list).RunAsync());

        Assert.Equal((0, 0), (again.EntriesVoided, again.Cardholders.Count));
        Assert.Equal(7, await StampsAsync(client, f.Customers["Card one"]));      // 6 carried + the new stamp, kept
        Assert.Equal(43, await StampsAsync(client, f.Customers["Card four"]));
    }

    [Fact]
    public async Task WithoutTheList_ItDoesNotRun_AndSaysSo()
    {
        var f = await ArrangeAsync("c");

        var result = await _factory.WithDbAsync(db => new LoyaltyCarryOver(db, "/nowhere/loyalty-cardholders.csv").RunAsync());

        Assert.Contains("missing", result.Skipped);
        Assert.False(await _factory.WithDbAsync(db => db.DataMigrations.AnyAsync()));
        Assert.Equal(6, await _factory.WithDbAsync(db => db.LoyaltyEntries.CountAsync(e => f.Customers.Values.Contains(e.CustomerId))));
    }

    [Fact]
    public async Task TheDevelopmentRedo_RestoresTheOldEntries_AndRunsTheRuleAgain()
    {
        var f = await ArrangeAsync("d");
        var list = WriteList("d", Rows);
        var client = await _factory.CreateOwnerClientAsync();
        await _factory.WithDbAsync(db => new LoyaltyCarryOver(db, list).RunAsync());

        await _factory.WithDbAsync(db => new LoyaltyCarryOver(db, list).UndoAsync());

        // Back to before: old entries live again, no cards, no marker.
        Assert.Equal(6, await _factory.WithDbAsync(db => db.LoyaltyEntries.CountAsync(e => f.Customers.Values.Contains(e.CustomerId))));
        Assert.False(await _factory.WithDbAsync(db => db.LoyaltyMemberships.AnyAsync(m => f.Customers.Values.Contains(m.CustomerId))));
        Assert.False(await _factory.WithDbAsync(db => db.DataMigrations.AnyAsync()));

        var again = await _factory.WithDbAsync(db => new LoyaltyCarryOver(db, list).RunAsync());
        Assert.Equal(4, again.Cardholders.Count);
        Assert.Equal(15, await StampsAsync(client, f.Customers["Card two"]));
    }

    [Fact]
    public void TheList_ReadsWhatASpreadsheetSaves_AndRefusesWhatItCannotRead()
    {
        var rows = CardholderList.Parse(
            "customer,has_card,card_number,position,estimate,last_reward_on\r\n" +
            "Irish,yes,4,,7,8/15/2026\r\n" +
            "Gaga,yes,0,,,\r\n" +
            "Prince (sample),no,,,0,11/8/2026\r\n" +
            ",, ,,,\r\n");

        Assert.Equal(
            [("Irish", true, 4, 7), ("Gaga", true, 1, 0), ("Prince (sample)", false, 1, 0)],
            rows.Select(r => (r.Customer, r.HasCard, r.CardNumber, r.Slots)));
        Assert.Throws<InvalidDataException>(() => CardholderList.Parse("customer,has_card,position,estimate\nIrish,maybe,,\n"));
        Assert.Throws<InvalidDataException>(() => CardholderList.Parse("customer,has_card,position,estimate\nIrish,yes,12,\n"));
        Assert.Throws<InvalidDataException>(() => CardholderList.Parse("customer,has_card,card_number,position,estimate\nIrish,yes,-1,,\n"));
    }

    [Fact]
    public void ThePublishedList_IsTheOwnersFile()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "docs", "import")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        Assert.Equal(
            File.ReadAllBytes(Path.Combine(root.FullName, "docs", "import", CardholderList.FileName)),
            File.ReadAllBytes(Path.Combine(root.FullName, "api", "Klowee.Api", "Data", CardholderList.FileName)));
    }
}
