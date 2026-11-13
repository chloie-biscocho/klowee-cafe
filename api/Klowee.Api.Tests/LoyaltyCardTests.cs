using Klowee.Api.Entities;
using Klowee.Api.Services;

namespace Klowee.Api.Tests;

/// <summary>The derived card: pure arithmetic over the ledger sum and the used rewards.</summary>
public class LoyaltyCardTests
{
    private static readonly Guid Half = Guid.NewGuid();
    private static readonly Guid Shot = Guid.NewGuid();
    private static readonly Guid Free = Guid.NewGuid();

    private static readonly CardReward[] Coffee =
    [
        new(Half, 5, "50% OFF", LoyaltyRewardType.PercentOff),
        new(Shot, 8, "EXTRA SHOT", LoyaltyRewardType.FreeAddOn),
        new(Free, 12, "FREE DRINK", LoyaltyRewardType.FreeDrink)
    ];

    private static string States(CardState card) =>
        string.Join(" ", card.Rewards.Select(r => $"{r.Cycle}:{r.Slot}={r.State.ToString()[0]}"));

    [Theory]
    // S, cycle, position, the current card's rewards then older cards' unused ones (L locked, A available)
    [InlineData(-3, 0, 0, "0:5=L 0:8=L 0:12=L")]          // a negative ledger floors at 0
    [InlineData(0, 0, 0, "0:5=L 0:8=L 0:12=L")]
    [InlineData(3, 0, 3, "0:5=L 0:8=L 0:12=L")]
    [InlineData(4, 0, 4, "0:5=A 0:8=L 0:12=L")]           // slot 4 filled: the 5th drink gets 50% off
    [InlineData(7, 0, 7, "0:5=A 0:8=A 0:12=L")]
    [InlineData(11, 0, 11, "0:5=A 0:8=A 0:12=A")]
    [InlineData(12, 1, 0, "1:5=L 1:8=L 1:12=L 0:5=A 0:8=A 0:12=A")]   // card restarts; unused rewards stay
    [InlineData(16, 1, 4, "1:5=A 1:8=L 1:12=L 0:5=A 0:8=A 0:12=A")]
    [InlineData(23, 1, 11, "1:5=A 1:8=A 1:12=A 0:5=A 0:8=A 0:12=A")]
    [InlineData(28, 2, 4, "2:5=A 2:8=L 2:12=L 0:5=A 0:8=A 0:12=A 1:5=A 1:8=A 1:12=A")]
    public void Derives_CyclePositionAndRewards_FromTheLedgerSum(int sum, int cycle, int position, string rewards)
    {
        var card = LoyaltyCard.For(sum, 12, Coffee, []);

        Assert.Equal(cycle, card.Cycle);
        Assert.Equal(position, card.Position);
        Assert.Equal(rewards, States(card));
    }

    [Fact]
    public void AUsedReward_IsUsedOnItsOwnCard_AndGoneFromTheAvailableList()
    {
        var redemptions = new[]
        {
            new CardRedemption(Guid.NewGuid(), Half, 0, null),
            new CardRedemption(Guid.NewGuid(), Shot, 0, Guid.NewGuid()),
            new CardRedemption(Guid.NewGuid(), Half, 1, null)
        };

        var card = LoyaltyCard.For(17, 12, Coffee, redemptions);

        // Card 2 (cycle 1): 50% used; card 1's free drink still waiting.
        Assert.Equal("1:5=U 1:8=L 1:12=L 0:12=A", States(card));
        Assert.Equal([(Free, 0)], card.Available.Select(v => (v.RewardId, v.Cycle)));
        Assert.NotNull(card.Rewards[0].RedemptionId);
    }

    [Fact]
    public void Available_IsOldestCardFirst()
    {
        var card = LoyaltyCard.For(16, 12, Coffee, []);

        Assert.Equal([(Half, 0), (Shot, 0), (Free, 0), (Half, 1)], card.Available.Select(v => (v.RewardId, v.Cycle)));
    }

    [Theory]
    [InlineData(LoyaltyMode.Inherit, false, true, "category")]
    [InlineData(LoyaltyMode.None, true, true, "none")]
    [InlineData(LoyaltyMode.Club, true, true, "item")]
    [InlineData(LoyaltyMode.Inherit, false, false, "none")]
    public void ClubOfADrink_ItemOverridesCategory(LoyaltyMode mode, bool itemHasClub, bool categoryHasClub, string expected)
    {
        var item = Guid.NewGuid();
        var category = Guid.NewGuid();

        var club = LoyaltyClubMap.Resolve(mode, itemHasClub ? item : null, categoryHasClub ? category : null);

        Assert.Equal(expected switch { "item" => item, "category" => category, _ => (Guid?)null }, club);
    }
}
