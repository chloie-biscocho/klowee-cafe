using Klowee.Api.Entities;

namespace Klowee.Api.Services;

/// <summary>
/// Turns snapshot prices into an order's money:
///   line total  = (unit price + add-on prices) × quantity
///   subtotal    = sum of line totals
///   discount    = None 0 · Percent subtotal × value% (half-up to the centavo)
///                 · Fixed the value · LoyaltyFree one unit of the cheapest
///                 line, add-ons included
///   total       = subtotal − discount, never below zero
/// The discount is capped at the subtotal whatever the input; whether a value
/// is allowed at all (0–100%, a reward available) is the order service's call.
/// </summary>
public class PricingService : IPricingService
{
    public PricingResult Price(IReadOnlyList<PricingLine> lines, DiscountType discountType, decimal discountValue)
    {
        var unitTotals = lines.Select(line => line.UnitPrice + line.AddOnPrices.Sum()).ToList();
        var lineTotals = lines.Select((line, i) => unitTotals[i] * line.Quantity).ToList();
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
        return new PricingResult(lineTotals, subtotal, discount, subtotal - discount, freeLine);
    }

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
