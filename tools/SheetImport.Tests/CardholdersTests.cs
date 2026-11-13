namespace Klowee.SheetImport.Tests;

/// <summary>The cardholder scan's rules (handover 16a): detection on fixture orders and the estimate.</summary>
public class CardholdersTests
{
    private static OrderFacts Order(string notes = "", string discount = "None", decimal value = 0, int day = 1, int coffees = 1,
        bool paid = true, string customer = "Irish", int shotsAtZero = 0, params string[] lineRewards) =>
        new(customer, new DateOnly(2026, 1, day), day, notes, discount, value, paid, coffees, shotsAtZero, lineRewards);

    [Theory]
    [InlineData("[import:Sept 2025:85] 100% loyalty card", 12, true)]
    [InlineData("100% off", 12, true)]
    [InlineData("FREE DRINK - will pay oat milk", 12, true)]
    [InlineData("50% loyalty card - with extra shot", 5, true)]
    [InlineData("50 % LOYALTY CARD", 5, true)]
    [InlineData("50% OFF", 5, false)]
    [InlineData("50 % with extra shot", 5, false)]
    [InlineData("free extra shot (10.1)", 8, true)]
    [InlineData("Free shot", 8, true)]
    public void Notes_TellTheRewardAndHowSureWeAre(string notes, int slot, bool strong)
    {
        var found = Cardholders.Detect(Order(Cardholders.StripMarker(notes)));

        var reward = Cardholders.Reward(found)!;
        Assert.Equal((slot, strong), (reward.Slot, reward.Strong));
    }

    [Fact]
    public void Discounts_ShotsAtZero_AndLineRewards_AreEvidenceToo()
    {
        Assert.Equal(12, Cardholders.Reward(Cardholders.Detect(Order(discount: "LoyaltyFree")))!.Slot);
        Assert.False(Cardholders.Reward(Cardholders.Detect(Order(discount: "Percent", value: 50)))!.Strong);
        Assert.Empty(Cardholders.Detect(Order(discount: "Percent", value: 10)));
        Assert.Equal(8, Cardholders.Reward(Cardholders.Detect(Order(shotsAtZero: 1)))!.Slot);
        Assert.Equal(5, Cardholders.Reward(Cardholders.Detect(Order(lineRewards: "PercentOff")))!.Slot);
        Assert.Empty(Cardholders.Detect(Order("with extra shot, oat milk")));          // a paid shot is not a reward
        Assert.Equal(12, Cardholders.Reward(Cardholders.Detect(Order("100% loyalty card + Free extra shot")))!.Slot);
    }

    [Theory]
    [InlineData(5, 0, 5)]
    [InlineData(5, 3, 8)]
    [InlineData(8, 2, 10)]
    [InlineData(12, 0, 0)]       // a free drink completes the card
    [InlineData(12, 7, 7)]
    [InlineData(5, 9, 2)]        // wraps past 12
    public void Estimate_IsTheRewardSlotPlusCoffeesSince_RoundTheCard(int slot, int since, int estimate)
    {
        Assert.Equal(estimate, Cardholders.Estimate(slot, since));
    }

    [Fact]
    public void Plan_CountsPaidCoffeesAfterTheLastReward_AndFlagsAPassedFreeDrink()
    {
        var row = Cardholders.Plan("Irish",
        [
            Order("50% loyalty card", day: 1),
            Order("100% loyalty card", day: 2, coffees: 2),     // the free drink, plus one paid coffee
            Order(day: 3, coffees: 3),
            Order(day: 4, coffees: 4, paid: false),             // unpaid: stamps after the carry-over, not now
            Order(day: 5, coffees: 0),                          // no Coffee Club drink
        ])!;

        Assert.Equal((12, new DateOnly(2026, 1, 2), 4, 4), (row.LastRewardSlot, row.LastRewardOn, row.CoffeesPaidSince, row.Estimate));
        Assert.True(row.HasCard);
        Assert.Equal("", row.Flag);

        var passed = Cardholders.Plan("MJ", [Order("50% loyalty card", day: 1), Order(day: 2, coffees: 9)])!;
        Assert.Contains("passed slot 12", passed.Flag);
    }

    [Fact]
    public void Plan_FlagsTheOwnersAndWeakEvidence_AndSkipsCustomersWithNone()
    {
        Assert.False(Cardholders.Plan("Kesha", [Order("100% loyalty card", customer: "Kesha")])!.HasCard);
        Assert.Contains("only evidence is one 50%", Cardholders.Plan("Sam", [Order("50% off", customer: "Sam")])!.Flag);
        Assert.Contains("no reward slot", Cardholders.Plan("Gaga", [Order("c/o kee payment & stamp")])!.Flag);
        Assert.Null(Cardholders.Plan("Walk-in", [Order("oat milk"), Order(discount: "Fixed", value: 20)]));
    }
}
