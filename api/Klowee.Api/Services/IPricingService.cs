using Klowee.Api.Entities;

namespace Klowee.Api.Services;

/// <summary>
/// A loyalty reward on one unit of a line.
/// <paramref name="Percent"/> is for PercentOff. <paramref name="AddOnPriceOnLine"/>
/// is for FreeAddOn: the add-on's snapshot price when the line already carries it
/// (one unit's copy is then free), or null when the reward adds it at ₱0.
/// </summary>
public record PricingReward(LoyaltyRewardType Type, decimal Percent = 0m, decimal? AddOnPriceOnLine = null);

/// <summary>One drink line as priced: its snapshot unit price, quantity, add-on prices and reward.</summary>
public record PricingLine(decimal UnitPrice, int Quantity, IReadOnlyList<decimal> AddOnPrices, PricingReward? Reward = null);

/// <param name="LineTotals">Per line, in input order, after the line's reward.</param>
/// <param name="RewardAmounts">Per line, what its reward took off (0 without one).</param>
/// <param name="Subtotal">The line totals added up: after rewards, before the order discount.</param>
/// <param name="FreeLineIndex">The line one unit of which an old LoyaltyFree discount covers; otherwise null.</param>
public record PricingResult(
    IReadOnlyList<decimal> LineTotals,
    IReadOnlyList<decimal> RewardAmounts,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    int? FreeLineIndex);

/// <summary>
/// Order arithmetic and nothing else: no database, no clock, no rules about
/// who may have which discount or reward. See decisions 016 and 020.
/// </summary>
public interface IPricingService
{
    PricingResult Price(IReadOnlyList<PricingLine> lines, DiscountType discountType, decimal discountValue);
}
