using System.Net;
using System.Net.Http.Json;
using Klowee.Api.Contracts.Customers;
using Klowee.Api.Contracts.Loyalty;
using Klowee.Api.Contracts.Menu;
using Klowee.Api.Contracts.Orders;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;
using static Klowee.Api.Tests.SalesFixture;

namespace Klowee.Api.Tests;

/// <summary>
/// Loyalty clubs through the API (decision 020), on The Coffee Club only: the
/// Matcha Club stays inactive in this class's database (see LoyaltyMatchaClubTests).
/// The rule numbers are handover 16's.
/// </summary>
public class LoyaltyClubTests : IClassFixture<KloweeApiFactory>
{
    private const string Suffix = "clubs";
    private readonly KloweeApiFactory _factory;

    public LoyaltyClubTests(KloweeApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, SalesMenu Menu)> SetupAsync() =>
        (await _factory.CreateOwnerClientAsync(), await SeedMenuAsync(_factory, Suffix));

    private Task<Guid> HalfOffAsync() => CoffeeRewardAsync(_factory, 5);
    private Task<Guid> ShotAsync() => CoffeeRewardAsync(_factory, 8);
    private Task<Guid> FreeDrinkAsync() => CoffeeRewardAsync(_factory, 12);

    private Task<List<LoyaltyRedemption>> RedemptionsAsync(Guid customerId, bool includeVoided = false) =>
        _factory.WithDbAsync(db => (includeVoided ? db.LoyaltyRedemptions.IgnoreQueryFilters() : db.LoyaltyRedemptions)
            .Where(r => r.CustomerId == customerId).ToListAsync());

    private static string? State(CustomerClubDto card, int slot, int cycle = 0) =>
        card.Rewards.FirstOrDefault(r => r.Slot == slot && r.Cycle == cycle)?.State;

    // ---- Rule 3: applying a reward ----

    [Fact]
    public async Task AnAvailableReward_IsApplied_UsedUp_AndShownOnTheLine()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee half off");
        await GiveStampsAsync(client, kee.Id, 4);
        Assert.Equal("available", State(await CardAsync(client, kee.Id), 5));

        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [RewardLine(m.Latte, await HalfOffAsync(), 1, m.OatMilk)]));

        Assert.Equal(110m, order.Total);                       // 160 + 30 oat, half the 160 off
        var reward = order.Items[0].Reward!;
        Assert.Equal(("50% OFF", LoyaltyRewardType.PercentOff, 80m), (reward.Label, reward.Type, reward.AmountOff));
        Assert.Equal("used", State(await CardAsync(client, kee.Id), 5));
        var redemption = Assert.Single(await RedemptionsAsync(kee.Id));
        Assert.Equal((order.Id, order.Items[0].Id, 0), (redemption.OrderId, redemption.OrderItemId, redemption.Cycle));
    }

    [Fact]
    public async Task ARewardThatIsNotAvailable_Is409()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee too early");
        await GiveStampsAsync(client, kee.Id, 3);

        var early = await PostOrderAsync(client, OrderBody(kee.Id, [RewardLine(m.Latte, await HalfOffAsync())]));
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);

        // Two lines can't both use the one 50% off.
        await GiveStampsAsync(client, kee.Id, 1);
        var twice = await PostOrderAsync(client, OrderBody(kee.Id,
            [RewardLine(m.Latte, await HalfOffAsync()), RewardLine(m.Americano, await HalfOffAsync())]));
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Empty(await RedemptionsAsync(kee.Id));
    }

    [Fact]
    public async Task ARewardOnADrinkOfAnotherClubOrNone_Is409()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee wrong card");
        await GiveStampsAsync(client, kee.Id, 11);
        var refresher = await AddDrinkAsync(_factory, Suffix, "Refresher", 120m);   // a category with no club

        var response = await PostOrderAsync(client, OrderBody(kee.Id, [RewardLine(refresher, await FreeDrinkAsync())]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AReward_AffectsOneUnitOfTheLine()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee two lattes");
        await GiveStampsAsync(client, kee.Id, 4);

        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [RewardLine(m.Latte, await HalfOffAsync(), 2)]));

        Assert.Equal(240m, order.Total);                       // 320, half of one 160 off
    }

    [Fact]
    public async Task FreeAddOn_AddsTheShotAtZero_OrFreesTheOneOnTheLine()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee shot");
        await GiveStampsAsync(client, kee.Id, 7);
        var shot = await ShotAsync();

        var added = await CreateOrderAsync(client, OrderBody(kee.Id, [RewardLine(m.Americano, shot)]));
        Assert.Equal(110m, added.Total);
        Assert.Equal(0m, added.Items[0].Reward!.AmountOff);    // given at ₱0; the label tells the barista

        await client.DeleteAsync($"/api/orders/{added.Id}");
        var onLine = await CreateOrderAsync(client, OrderBody(kee.Id, [RewardLine(m.Americano, shot, 1, m.ExtraShot)]));
        Assert.Equal(110m, onLine.Total);                      // 110 + 25 shot, the shot free
        Assert.Equal(25m, onLine.Items[0].Reward!.AmountOff);
    }

    [Fact]
    public async Task FreeDrink_StillChargesTheOatMilk()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee free oat");
        await GiveStampsAsync(client, kee.Id, 11);

        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [RewardLine(m.Latte, await FreeDrinkAsync(), 1, m.OatMilk)]));

        Assert.Equal(30m, order.Total);
        Assert.Equal(PaymentStatus.Unpaid, order.Status);
    }

    [Fact]
    public async Task ReSaving_KeepsTheRedemption_RemovingTheRewardOrLineOrOrder_GivesItBack()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee resave");
        await GiveStampsAsync(client, kee.Id, 4);
        var half = await HalfOffAsync();

        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [RewardLine(m.Latte, half)]));
        var first = Assert.Single(await RedemptionsAsync(kee.Id));

        // Re-saving (a note added) doesn't need a second reward, and keeps the same row.
        await EnsureAsync(await client.PutAsJsonAsync($"/api/orders/{order.Id}",
            OrderBody(kee.Id, [RewardLine(m.Latte, half)], notes: "less ice")));
        Assert.Equal(first.Id, Assert.Single(await RedemptionsAsync(kee.Id)).Id);

        // Removing the reward gives it back.
        await EnsureAsync(await client.PutAsJsonAsync($"/api/orders/{order.Id}", OrderBody(kee.Id, [Line(m.Latte)])));
        Assert.Empty(await RedemptionsAsync(kee.Id));
        Assert.Equal("available", State(await CardAsync(client, kee.Id), 5));

        // Deleting the line that carried it gives it back.
        await EnsureAsync(await client.PutAsJsonAsync($"/api/orders/{order.Id}",
            OrderBody(kee.Id, [Line(m.Americano), RewardLine(m.Latte, half)])));
        Assert.Single(await RedemptionsAsync(kee.Id));
        await EnsureAsync(await client.PutAsJsonAsync($"/api/orders/{order.Id}", OrderBody(kee.Id, [Line(m.Americano)])));
        Assert.Empty(await RedemptionsAsync(kee.Id));

        // Deleting the order gives it back.
        await EnsureAsync(await client.PutAsJsonAsync($"/api/orders/{order.Id}", OrderBody(kee.Id, [RewardLine(m.Latte, half)])));
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/orders/{order.Id}")).StatusCode);
        Assert.Empty(await RedemptionsAsync(kee.Id));
        Assert.Equal(3, (await RedemptionsAsync(kee.Id, includeVoided: true)).Count);   // one per time it went on; the history stays
    }

    [Fact]
    public async Task RewardsAndStamps_BelongToTheDrinker_WhoeverPays()
    {
        var (client, m) = await SetupAsync();
        var prince = await CreateCardholderAsync(_factory, client, "Prince drinker");
        var kee = await CreateCardholderAsync(_factory, client, "Kee payer");
        await GiveStampsAsync(client, prince.Id, 4);

        var order = await CreateOrderAsync(client, OrderBody(prince.Id, [RewardLine(m.Latte, await HalfOffAsync())], payerId: kee.Id));
        await EnsureAsync(await PayAsync(client, order.Id, order.Total));

        Assert.Equal(5, await StampsAsync(client, prince.Id));
        Assert.Equal(0, await StampsAsync(client, kee.Id));
    }

    // ---- Rule 5: an order with payments is locked, rewards included ----

    [Fact]
    public async Task AnOrderWithPayments_CannotDropOrAddARewards()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee locked reward");
        await GiveStampsAsync(client, kee.Id, 4);
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [RewardLine(m.Latte, await HalfOffAsync())]));
        await EnsureAsync(await PayAsync(client, order.Id, 10m));

        var response = await client.PutAsJsonAsync($"/api/orders/{order.Id}", OrderBody(kee.Id, [Line(m.Latte)]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(await RedemptionsAsync(kee.Id));
    }

    // ---- Rule 2: stamping ----

    [Fact]
    public async Task APaidOrder_StampsOnePerClubDrink_RewardDrinksIncluded_OthersNothing()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee stamping");
        await GiveStampsAsync(client, kee.Id, 4);
        var refresher = await AddDrinkAsync(_factory, Suffix, "Lemonade", 90m);

        var order = await CreateOrderAsync(client, OrderBody(kee.Id,
            [RewardLine(m.Latte, await HalfOffAsync()), Line(m.Americano, 2), Line(refresher, 3)]));
        Assert.Equal(3, (await CardAsync(client, kee.Id)).PendingDrinks);   // the lemonades earn nothing
        await EnsureAsync(await PayAsync(client, order.Id, order.Total));

        var earned = Assert.Single(await LedgerAsync(client, kee.Id), e => e.Reason == LoyaltyReason.Earned);
        Assert.Equal((3, LoyaltyClub.CoffeeClubId, "The Coffee Club"), (earned.Delta, earned.ClubId, earned.ClubName));
        var card = await CardAsync(client, kee.Id);
        Assert.Equal((7, 0), (card.Position, card.PendingDrinks));
    }

    [Fact]
    public async Task AfterSlotTwelve_TheCardStartsAgain_AndUnusedRewardsStay()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee restart");
        await GiveStampsAsync(client, kee.Id, 11);

        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [Line(m.Americano, 3)]));
        await EnsureAsync(await PayAsync(client, order.Id, order.Total));

        var card = await CardAsync(client, kee.Id);
        Assert.Equal((1, 2), (card.Cycle, card.Position));
        Assert.Equal("locked", State(card, 5, cycle: 1));
        Assert.Equal("available", State(card, 12, cycle: 0));   // card 1's free drink, never used, still there
    }

    // ---- Manual redemptions ----

    [Fact]
    public async Task ARewardCanBeMarkedUsedByHand_AndMadeAvailableAgain()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee paper card");
        await GiveStampsAsync(client, kee.Id, 6);
        var half = await HalfOffAsync();
        var url = $"/api/customers/{kee.Id}/loyalty/redemptions";

        var marked = await client.PostAsJsonAsync(url, new { clubId = LoyaltyClub.CoffeeClubId, rewardId = half, cycle = 0, note = "paper card" });
        await EnsureAsync(marked);
        var redemption = (await marked.Content.ReadFromJsonAsync<LoyaltyRedemptionDto>(KloweeApiFactory.Json))!;
        Assert.Equal("used", State(await CardAsync(client, kee.Id), 5));

        Assert.Equal(HttpStatusCode.Conflict,      // already used
            (await client.PostAsJsonAsync(url, new { clubId = LoyaltyClub.CoffeeClubId, rewardId = half, cycle = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,      // not earned: slot 8 needs 7 stamps
            (await client.PostAsJsonAsync(url, new { clubId = LoyaltyClub.CoffeeClubId, rewardId = await ShotAsync(), cycle = 0 })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{url}/{redemption.Id}")).StatusCode);
        Assert.Equal("available", State(await CardAsync(client, kee.Id), 5));

        // One an order used can't be undone here.
        var order = await CreateOrderAsync(client, OrderBody(kee.Id, [RewardLine(m.Latte, half)]));
        var used = (await CardAsync(client, kee.Id)).Rewards.Single(r => r.Slot == 5 && r.Cycle == 0);
        Assert.Equal(order.Id, used.OrderId);
        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"{url}/{used.RedemptionId}")).StatusCode);
    }

    // ---- Rule 6: merge ----

    [Fact]
    public async Task Merge_AddsStampsPerClub_AndKeepsEveryUsedReward()
    {
        var (client, m) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee merge card");
        var dupe = await CreateCardholderAsync(_factory, client, "Kee card (sheet)");
        var half = await HalfOffAsync();
        await GiveStampsAsync(client, kee.Id, 6);
        await GiveStampsAsync(client, dupe.Id, 5);
        foreach (var who in new[] { kee.Id, dupe.Id })
        {
            await EnsureAsync(await client.PostAsJsonAsync($"/api/customers/{who}/loyalty/redemptions",
                new { clubId = LoyaltyClub.CoffeeClubId, rewardId = half, cycle = 0 }));
        }

        await EnsureAsync(await client.PostAsync($"/api/customers/{dupe.Id}/merge-into/{kee.Id}", null));

        Assert.Equal(11, await StampsAsync(client, kee.Id));
        var redemptions = await RedemptionsAsync(kee.Id);
        Assert.Equal([0, 1], redemptions.Select(r => r.Cycle).Order());     // both 50% offs survive, one per card
        var card = await CardAsync(client, kee.Id);
        Assert.Equal(("used", "available", "available"), (State(card, 5), State(card, 8), State(card, 12)));
    }

    // ---- Ledger and adjust (rule 7) ----

    [Fact]
    public async Task TheLedger_FiltersByClub()
    {
        var (client, _) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee ledger club");
        await GiveStampsAsync(client, kee.Id, 2);

        Assert.Single(await LedgerAsync(client, kee.Id, LoyaltyClub.CoffeeClubId));
        Assert.Empty(await LedgerAsync(client, kee.Id, LoyaltyClub.MatchaClubId));
    }

    // ---- The clubs themselves ----

    [Fact]
    public async Task Clubs_AreListedWithRewardsCategoriesAndMembers()
    {
        var (client, _) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee member");
        await GiveStampsAsync(client, kee.Id, 1);

        var clubs = (await client.GetFromJsonAsync<List<LoyaltyClubDto>>("/api/loyalty/clubs", KloweeApiFactory.Json))!;

        Assert.Equal(["The Coffee Club", "The Matcha Club"], clubs.Select(c => c.Name));
        var coffee = clubs[0];
        Assert.True(coffee.IsActive);
        Assert.False(clubs[1].IsActive);
        Assert.Equal([5, 8, 12], coffee.Rewards.Select(r => r.Slot));
        Assert.Equal([LoyaltyRewardType.PercentOff, LoyaltyRewardType.FreeAddOn, LoyaltyRewardType.FreeDrink], coffee.Rewards.Select(r => r.Type));
        Assert.NotEmpty(coffee.CategoryIds);
        Assert.True(coffee.MemberCount >= 1);
    }

    [Fact]
    public async Task AClub_SavesRewardsBySlot_AndItsCategories_AndIsDatedOnActivation()
    {
        var (client, _) = await SetupAsync();
        var categories = (await client.GetFromJsonAsync<List<MenuCategoryDto>>("/api/menu/categories", KloweeApiFactory.Json))!;
        var tea = await EnsureCategoryAsync(client, "Tea clubs");

        var created = await client.PostAsJsonAsync("/api/loyalty/clubs", new
        {
            name = "The Tea Club", style = "Matcha", slotCount = 10, isActive = false, sortOrder = 3,
            rewards = new object[]
            {
                new { slot = 5, type = "PercentOff", percent = 50, label = "50% OFF" },
                new { slot = 10, type = "FreeDrink", label = "FREE DRINK" }
            },
            categoryIds = new[] { tea }
        });
        await EnsureAsync(created);
        var club = (await created.Content.ReadFromJsonAsync<LoyaltyClubDto>(KloweeApiFactory.Json))!;
        Assert.Equal([tea], club.CategoryIds);
        Assert.Null(club.ActivatedOn);
        var fifth = club.Rewards.Single(r => r.Slot == 5).Id;

        // Slot 5 changes in place (same reward, so its uses would stay), 10 goes, 7 arrives.
        var updated = await client.PutAsJsonAsync($"/api/loyalty/clubs/{club.Id}", new
        {
            name = "The Tea Club", style = "Matcha", slotCount = 10, isActive = true, sortOrder = 3,
            rewards = new object[]
            {
                new { slot = 5, type = "PercentOff", percent = 40, label = "40% OFF" },
                new { slot = 7, type = "FreeDrink", label = "FREE TEA" }
            },
            categoryIds = Array.Empty<Guid>()
        });
        await EnsureAsync(updated);
        club = (await updated.Content.ReadFromJsonAsync<LoyaltyClubDto>(KloweeApiFactory.Json))!;
        Assert.Equal([(5, "40% OFF"), (7, "FREE TEA")], club.Rewards.Select(r => (r.Slot, r.Label)));
        Assert.Equal(fifth, club.Rewards[0].Id);
        Assert.Empty(club.CategoryIds);
        Assert.Equal(Today, club.ActivatedOn);
        Assert.Null((await client.GetFromJsonAsync<List<MenuCategoryDto>>("/api/menu/categories", KloweeApiFactory.Json))!
            .Single(c => c.Id == tea).LoyaltyClubId);
        Assert.Equal(categories.Count + 1, (await client.GetFromJsonAsync<List<MenuCategoryDto>>("/api/menu/categories", KloweeApiFactory.Json))!.Count);

        // Bad rewards are 400s.
        foreach (var rewards in new object[][]
                 {
                     [new { slot = 11, type = "FreeDrink", label = "PAST THE END" }],
                     [new { slot = 3, type = "PercentOff", label = "NO PERCENT" }],
                     [new { slot = 3, type = "FreeAddOn", label = "NO ADD-ON" }],
                     [new { slot = 3, type = "FreeDrink", label = "A" }, new { slot = 3, type = "FreeDrink", label = "B" }]
                 })
        {
            var bad = await client.PutAsJsonAsync($"/api/loyalty/clubs/{club.Id}",
                new { name = "The Tea Club", style = "Matcha", slotCount = 10, isActive = true, rewards });
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }

        // No ledger yet, so it can be deleted.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/loyalty/clubs/{club.Id}")).StatusCode);
    }

    [Fact]
    public async Task AClubWithStamps_CannotBeDeleted_OnlyDeactivated()
    {
        var (client, _) = await SetupAsync();
        var kee = await CreateCardholderAsync(_factory, client, "Kee keeps club");
        await GiveStampsAsync(client, kee.Id, 1);

        Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/loyalty/clubs/{LoyaltyClub.CoffeeClubId}")).StatusCode);
    }

    [Fact]
    public async Task MenuCategoriesItemsAndVersionItems_CarryTheClub()
    {
        var (client, m) = await SetupAsync();
        var tea = await EnsureCategoryAsync(client, "Tea items clubs");

        // A category's club is set by its PUT.
        await EnsureAsync(await client.PutAsJsonAsync($"/api/menu/categories/{tea}",
            new { name = "Tea items clubs", sortOrder = 7, loyaltyClubId = LoyaltyClub.MatchaClubId }));
        var category = (await client.GetFromJsonAsync<List<MenuCategoryDto>>("/api/menu/categories", KloweeApiFactory.Json))!.Single(c => c.Id == tea);
        Assert.Equal(LoyaltyClub.MatchaClubId, category.LoyaltyClubId);

        // An item can opt out, or name its own club (which then must be given).
        var item = await client.PostAsJsonAsync("/api/menu/items", new { name = "Hojicha clubs", categoryId = tea, loyaltyMode = "None" });
        await EnsureAsync(item);
        var created = (await item.Content.ReadFromJsonAsync<MenuItemDto>(KloweeApiFactory.Json))!;
        Assert.Equal((LoyaltyMode.None, (Guid?)null), (created.LoyaltyMode, created.LoyaltyClubId));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/menu/items/{created.Id}",
            new { name = "Hojicha clubs", categoryId = tea, loyaltyMode = "Club" })).StatusCode);

        // A version item says which club the drink stamps, override applied.
        var office = (await client.GetFromJsonAsync<List<MenuVersionSummaryDto>>("/api/menu/versions", KloweeApiFactory.Json))!
            .Single(v => v.Name == $"Office menu {Suffix}");
        var version = (await client.GetFromJsonAsync<MenuVersionDetailDto>($"/api/menu/versions/{office.Id}", KloweeApiFactory.Json))!;
        Assert.Equal(LoyaltyClub.CoffeeClubId, version.Items.Single(i => i.Id == m.Latte).LoyaltyClubId);
    }

    private static async Task<Guid> EnsureCategoryAsync(HttpClient client, string name)
    {
        var existing = (await client.GetFromJsonAsync<List<MenuCategoryDto>>("/api/menu/categories", KloweeApiFactory.Json))!
            .FirstOrDefault(c => c.Name == name);
        if (existing is not null)
        {
            return existing.Id;
        }

        var response = await client.PostAsJsonAsync("/api/menu/categories", new { name, sortOrder = 7 });
        await EnsureAsync(response);
        return (await response.Content.ReadFromJsonAsync<MenuCategoryDto>(KloweeApiFactory.Json))!.Id;
    }
}
