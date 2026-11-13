using Klowee.Api.Entities;

namespace Klowee.Api.Services;

/// <summary>
/// Turns snapshot prices into an order's money, in this order:
///   line        = (unit price + add-on prices) × quantity, less the line's reward:
///                 PercentOff  that percent of one unit's drink price (add-ons charged)
///                 FreeAddOn   one unit's copy of the add-on, if the line carries it;
///                             otherwise the add-on is given at ₱0 and nothing comes off
///                 FreeDrink   one unit's drink price (add-ons still charged)
///   subtotal    = sum of line totals
///   discount    = None 0 · Percent subtotal × value% · Fixed the value
///                 · LoyaltyFree (history only) one unit of the cheapest line, add-ons included
///   total       = subtotal − discount, never below zero
/// Percentages round half-up to the centavo. A reward never takes a line below
/// zero and the discount is capped at the subtotal; whether a reward or discount
/// is allowed at all is the order service's call.
/// </summary>
public class PricingService : IPricingService
{
    public PricingResult Price(IReadOnlyList<PricingLine> lines, DiscountType discountType, decimal discountValue)
    {
        var unitTotals = lines.Select(line => line.UnitPrice + line.AddOnPrices.Sum()).ToList();
        var gross = lines.Select((line, i) => unitTotals[i] * line.Quantity).ToList();
        var rewards = lines.Select((line, i) => Math.Clamp(RewardAmount(line), 0m, gross[i])).ToList();
        var lineTotals = gross.Select((g, i) => g - rewards[i]).ToList();
        var subtotal = lineTotals.Sum();

        int? freeLine = null;
        var discount = discountType switch
        {
            DiscountType.Percent => RoundHalfUp(subtotal * discountValue / 100m),
            DiscountType.Fixed => discountValue,
            DiscountType.LoyaltyFree => FreeDrink(unitTotals, out freeLine),
            _ => 0m
        };

        discount = Math.Clamp(discount, 0m, subtotal);
        return new PricingResult(lineTotals, rewards, subtotal, discount, subtotal - discount, freeLine);
    }

    /// <summary>What a line's reward takes off one unit.</summary>
    private static decimal RewardAmount(PricingLine line) => line.Reward switch
    {
        null => 0m,
        { Type: LoyaltyRewardType.PercentOff } r => RoundHalfUp(line.UnitPrice * r.Percent / 100m),
        { Type: LoyaltyRewardType.FreeAddOn } r => r.AddOnPriceOnLine ?? 0m,
        { Type: LoyaltyRewardType.FreeDrink } => line.UnitPrice,
        _ => 0m
    };

    /// <summary>One unit of the cheapest line; on a tie, the first such line.</summary>
    private static decimal FreeDrink(List<decimal> unitTotals, out int? freeLine)
    {
        freeLine = null;
        for (var i = 0; i < unitTotals.Count; i++)
        {
            if (freeLine is null || unitTotals[i] < unitTotals[freeLine.Value])
            {
                freeLine = i;
            }
        }

        return freeLine is null ? 0m : unitTotals[freeLine.Value];
    }

    /// <summary>Half-up to the centavo: ₱54.375 → ₱54.38 (amounts here are never negative).</summary>
    public static decimal RoundHalfUp(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
