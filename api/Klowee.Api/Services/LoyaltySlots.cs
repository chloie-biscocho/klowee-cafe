namespace Klowee.Api.Services;

/// <summary>Where a stamp came from: a paid order, the paper-card carry-over, or a hand adjustment.</summary>
public enum SlotSource
{
    Order,
    CarriedOver,
    Adjustment
}

/// <summary>One live ledger entry, as the slot builder needs it: oldest first.</summary>
/// <param name="Date">The order's date (business time zone) for an order's entry; null otherwise.</param>
public sealed record SlotEntry(int Delta, SlotSource Source, DateOnly? Date, Guid? OrderId);

/// <summary>One slot on a card. Unfilled slots carry no date, order or source.</summary>
public sealed record StampedSlot(int Slot, bool Filled, DateOnly? Date, Guid? OrderId, SlotSource? Source);

/// <summary>One card: its number (1-based), whether it is the current one, and its slots 1..N.</summary>
public sealed record CardSlots(int CardNumber, bool IsCurrent, IReadOnlyList<StampedSlot> Slots);

/// <summary>
/// The stamps laid out on cards, like the dated paper card (handover 16b).
/// Pure: it replays the ledger, oldest entry first.
///   a positive entry of n fills the next n slots, all with its date and order
///     (an adjustment or the carry-over fills them undated: dates are never invented);
///   a negative entry removes the n most recently filled slots; any excess is a
///     debt the next stamps pay first, so the filled count is always the ledger
///     sum floored at 0, the same number <see cref="LoyaltyCard"/> derives
///     cycle and position from;
///   overall slot k is slot ((k − 1) mod N) + 1 of card ⌊(k − 1) ÷ N⌋ + 1.
/// Every card from 1 to the current one is returned.
/// </summary>
public static class LoyaltySlots
{
    public static IReadOnlyList<CardSlots> Build(IReadOnlyList<SlotEntry> entries, int slotCount)
    {
        if (slotCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount), "A card needs at least one slot.");
        }

        var filled = new List<SlotEntry>();
        var debt = 0;
        foreach (var entry in entries)
        {
            if (entry.Delta > 0)
            {
                var repaid = Math.Min(debt, entry.Delta);
                debt -= repaid;
                for (var i = 0; i < entry.Delta - repaid; i++)
                {
                    filled.Add(entry);
                }
            }
            else if (entry.Delta < 0)
            {
                var remove = -entry.Delta;
                var removed = Math.Min(remove, filled.Count);
                filled.RemoveRange(filled.Count - removed, removed);
                debt += remove - removed;
            }
        }

        var current = filled.Count / slotCount;   // 0-based, as LoyaltyCard's cycle
        return Enumerable.Range(0, current + 1)
            .Select(card => new CardSlots(
                card + 1,
                card == current,
                Enumerable.Range(1, slotCount).Select(slot =>
                {
                    var index = card * slotCount + slot - 1;
                    if (index >= filled.Count)
                    {
                        return new StampedSlot(slot, false, null, null, null);
                    }

                    var stamp = filled[index];
                    return stamp.Source == SlotSource.Order
                        ? new StampedSlot(slot, true, stamp.Date, stamp.OrderId, SlotSource.Order)
                        : new StampedSlot(slot, true, null, null, stamp.Source);
                }).ToList()))
            .ToList();
    }
}
