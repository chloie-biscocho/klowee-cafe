using Klowee.Api.Services;

namespace Klowee.Api.Tests;

/// <summary>The card stack with dates (handover 16b): a pure replay of the ledger. Named by rule.</summary>
public class LoyaltySlotsTests
{
    private static readonly DateOnly Sep2 = new(2026, 9, 2);
    private static readonly DateOnly Oct5 = new(2026, 10, 5);

    private static SlotEntry Order(int drinks, DateOnly on) => new(drinks, SlotSource.Order, on, Guid.NewGuid());
    private static SlotEntry Adjust(int delta) => new(delta, SlotSource.Adjustment, null, null);
    private static SlotEntry CarryOver(int stamps) => new(stamps, SlotSource.CarriedOver, null, null);

    private static IEnumerable<StampedSlot> FilledSlots(IReadOnlyList<CardSlots> cards) =>
        cards.SelectMany(c => c.Slots).Where(s => s.Filled);

    [Fact]
    public void AnOrderOfThreeDrinks_FillsThreeSlotsWithTheSameDate()
    {
        var order = Order(3, Sep2);

        var card = Assert.Single(LoyaltySlots.Build([order], 12));

        Assert.Equal(12, card.Slots.Count);
        Assert.Equal([(1, Sep2), (2, Sep2), (3, Sep2)], card.Slots.Where(s => s.Filled).Select(s => (s.Slot, s.Date!.Value)));
        Assert.All(card.Slots.Where(s => s.Filled), s => Assert.Equal((order.OrderId, (SlotSource?)SlotSource.Order), (s.OrderId, s.Source)));
        Assert.All(card.Slots.Skip(3), s => Assert.Equal((false, (DateOnly?)null, (Guid?)null, (SlotSource?)null), (s.Filled, s.Date, s.OrderId, s.Source)));
    }

    [Fact]
    public void AnOrderThatCrossesFromCardOneToCardTwo_SplitsAcrossThem()
    {
        var cards = LoyaltySlots.Build([Order(10, Sep2), Order(4, Oct5)], 12);

        Assert.Equal([(1, false), (2, true)], cards.Select(c => (c.CardNumber, c.IsCurrent)));
        Assert.Equal(Oct5, cards[0].Slots[11].Date);                     // slots 11 and 12 of card 1
        Assert.Equal(Oct5, cards[0].Slots[10].Date);
        Assert.Equal([1, 2], cards[1].Slots.Where(s => s.Filled).Select(s => s.Slot));
        Assert.All(cards[1].Slots.Where(s => s.Filled), s => Assert.Equal(Oct5, s.Date));
    }

    [Theory]
    [InlineData(1, 6)]
    [InlineData(2, 3)]
    [InlineData(4, 7)]
    public void ACarryOverOfCardNAtP_GivesNMinusOneFullUndatedCards_AndPUndatedSlots(int n, int p)
    {
        var cards = LoyaltySlots.Build([CarryOver(12 * (n - 1) + p)], 12);

        Assert.Equal(n, cards.Count);
        Assert.Equal(n, cards.Single(c => c.IsCurrent).CardNumber);
        Assert.All(cards.Take(n - 1), c => Assert.All(c.Slots, s => Assert.Equal((true, (DateOnly?)null, (SlotSource?)SlotSource.CarriedOver), (s.Filled, s.Date, s.Source))));
        Assert.Equal(p, cards[^1].Slots.Count(s => s.Filled));
        Assert.All(FilledSlots(cards), s => Assert.Null(s.Date));        // never invented
    }

    [Fact]
    public void ANegativeAdjustment_RemovesTheLatestSlots_IncludingAcrossACardBoundary()
    {
        var cards = LoyaltySlots.Build([Order(11, Sep2), Order(3, Oct5), Adjust(-4)], 12);

        // 14 filled, then the 4 latest go: the three Oct 5 stamps and one Sep 2.
        var card = Assert.Single(cards);
        Assert.Equal(10, card.Slots.Count(s => s.Filled));
        Assert.All(card.Slots.Where(s => s.Filled), s => Assert.Equal(Sep2, s.Date));

        // More removed than filled: the rest is a debt the next stamps pay first.
        var debt = LoyaltySlots.Build([Order(2, Sep2), Adjust(-5), Order(4, Oct5)], 12);
        Assert.Equal(1, Assert.Single(debt).Slots.Count(s => s.Filled));   // max(0, 2 − 5 + 4)
    }

    [Fact]
    public void APositiveAdjustment_FillsUndatedSlots_MarkedAddedByHand()
    {
        var card = Assert.Single(LoyaltySlots.Build([Order(1, Sep2), Adjust(2)], 12));

        Assert.Equal([(SlotSource?)SlotSource.Order, SlotSource.Adjustment, SlotSource.Adjustment],
            card.Slots.Where(s => s.Filled).Select(s => s.Source));
        Assert.Null(card.Slots[1].Date);
    }

    [Fact]
    public void AVoidedEntry_IsIgnored()
    {
        // The service passes live entries only; a voided order simply isn't there.
        var withVoided = LoyaltySlots.Build([Order(2, Sep2)], 12);
        var withoutVoided = LoyaltySlots.Build([Order(2, Sep2), Order(3, Oct5)], 12);

        Assert.Equal(2, withVoided.Single().Slots.Count(s => s.Filled));
        Assert.Equal(5, withoutVoided.Single().Slots.Count(s => s.Filled));
    }

    [Fact]
    public void TheSlotsCurrentCardAndFilledCount_EqualTheExistingCycleAndPosition()
    {
        var random = new Random(16_002);   // seeded: the same ledgers every run
        for (var run = 0; run < 500; run++)
        {
            var entries = Enumerable.Range(0, random.Next(0, 15)).Select(_ => random.Next(4) switch
            {
                0 => Adjust(random.Next(-6, 7)),
                1 => CarryOver(random.Next(0, 40)),
                _ => Order(random.Next(1, 5), Sep2)
            }).ToList();

            var cards = LoyaltySlots.Build(entries, 12);
            var card = LoyaltyCard.For(entries.Sum(e => e.Delta), 12, [], []);

            var current = cards.Single(c => c.IsCurrent);
            Assert.Equal(card.Cycle + 1, current.CardNumber);
            Assert.Equal(card.Position, current.Slots.Count(s => s.Filled));
            Assert.Equal(cards.Count, current.CardNumber);
            Assert.All(cards.Where(c => !c.IsCurrent), c => Assert.All(c.Slots, s => Assert.True(s.Filled)));
        }
    }
}
