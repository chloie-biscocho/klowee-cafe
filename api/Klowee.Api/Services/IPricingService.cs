using Klowee.Api.Entities;

namespace Klowee.Api.Services;

/// <summary>One drink line as priced: its snapshot unit price, quantity and add-on prices.</summary>
public record PricingLine(decimal UnitPrice, int Quantity, IReadOnlyList<decimal> AddOnPrices);

/// <param name="LineTotals">Per line, in input order.</param>
/// <param name="FreeLineIndex">The line one unit of which a LoyaltyFree discount covers; otherwise null.</param>
public record PricingResult(
    IReadOnlyList<decimal> LineTotals,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    int? FreeLineIndex);

/// <summary>
/// Order arithmetic and nothing else: no database, no clock, no rules about
/// who may have which discount. See docs/decisions/016-orders-and-payments-model.md.
/// </summary>
public interface IPricingService
{
    PricingResult Price(IReadOnlyList<PricingLine> lines, DiscountType discountType, decimal discountValue);
}
