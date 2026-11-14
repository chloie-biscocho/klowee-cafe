using Klowee.Api.Entities;

namespace Klowee.Api.Services;

public enum RewardState
{
    Locked,
    Available,
    Used
}

/// <summary>A reward printed on the card (one per slot).</summary>
public sealed record CardReward(Guid RewardId, int Slot, string Label, LoyaltyRewardType Type);

/// <summary>A reward already used in a given pass round the card.</summary>
public sealed record CardRedemption(Guid Id, Guid RewardId, int Cycle, Guid? OrderId);

/// <summary>One reward in one cycle, and where it stands.</summary>
public sealed record CardVoucher(
    Guid RewardId,
    int Slot,
    string Label,
    LoyaltyRewardType Type,
    int Cycle,
    RewardState State,
    Guid? RedemptionId,
    Guid? OrderId);

/// <param name="Stamps">S: the ledger's sum for this club, floored at 0.</param>
/// <param name="Position">Slots filled on the current card (0 to SlotCount − 1).</param>
/// <param name="Rewards">
/// The current card's rewards, each locked, available or used, followed by any
/// reward from an earlier card that was earned and never used (they don't expire).
/// </param>
public sealed record CardState(int Stamps, int SlotCount, int Cycle, int Position, IReadOnlyList<CardVoucher> Rewards)
{
    /// <summary>Available vouchers, oldest card first: the order they are used in.</summary>
    public IEnumerable<CardVoucher> Available => Rewards.Where(r => r.State == RewardState.Available).OrderBy(r => r.Cycle).ThenBy(r => r.Slot);
}

/// <summary>
/// A stamp card derived from a ledger (decision 020). Nothing here is stored:
///   cycle    = ⌊S ÷ slots⌋, position = S mod slots
///   earned   a reward on slot s in cycle c once S ≥ c × slots + (s − 1),
///            i.e. for the drink after the slot before it is filled
///   used     a live redemption exists for (reward, c)
///   available = earned and not used, in any cycle
/// Pure: no database, no clock.
/// </summary>
public static class LoyaltyCard
{
    public static bool IsEarned(int stamps, int slotCount, int slot, int cycle) =>
        Math.Max(0, stamps) >= cycle * slotCount + (slot - 1);

    /// <summary>Every reward on one card (cycle), each locked, available or used: what a card in the stack shows.</summary>
    public static IReadOnlyList<CardVoucher> RewardsOfCycle(
        int ledgerSum,
        int slotCount,
        IReadOnlyList<CardReward> rewards,
        IReadOnlyList<CardRedemption> redemptions,
        int cycle)
    {
        var stamps = Math.Max(0, ledgerSum);
        return rewards.OrderBy(r => r.Slot).Select(reward =>
        {
            var redemption = redemptions.FirstOrDefault(r => r.RewardId == reward.RewardId && r.Cycle == cycle);
            var state = redemption is not null ? RewardState.Used
                : IsEarned(stamps, slotCount, reward.Slot, cycle) ? RewardState.Available
                : RewardState.Locked;
            return new CardVoucher(reward.RewardId, reward.Slot, reward.Label, reward.Type, cycle, state, redemption?.Id, redemption?.OrderId);
        }).ToList();
    }

    public static CardState For(
        int ledgerSum,
        int slotCount,
        IReadOnlyList<CardReward> rewards,
        IReadOnlyList<CardRedemption> redemptions)
    {
        if (slotCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(slotCount), "A card needs at least one slot.");
        }

        var stamps = Math.Max(0, ledgerSum);
        var cycle = stamps / slotCount;
        var position = stamps % slotCount;
        var used = redemptions
            .GroupBy(r => (r.RewardId, r.Cycle))
            .ToDictionary(g => g.Key, g => g.First());

        var vouchers = new List<CardVoucher>();
        var ordered = rewards.OrderBy(r => r.Slot).ToList();

        // The current card first, every reward on it; then older cards' unused ones.
        foreach (var c in new[] { cycle }.Concat(Enumerable.Range(0, cycle)))
        {
            foreach (var reward in ordered)
            {
                used.TryGetValue((reward.RewardId, c), out var redemption);
                var state = redemption is not null ? RewardState.Used
                    : IsEarned(stamps, slotCount, reward.Slot, c) ? RewardState.Available
                    : RewardState.Locked;

                if (c == cycle || state == RewardState.Available)
                {
                    vouchers.Add(new CardVoucher(
                        reward.RewardId, reward.Slot, reward.Label, reward.Type, c, state,
                        redemption?.Id, redemption?.OrderId));
                }
            }
        }

        return new CardState(stamps, slotCount, cycle, position, vouchers);
    }
}
