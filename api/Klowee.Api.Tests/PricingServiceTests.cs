using Klowee.Api.Entities;
using Klowee.Api.Services;

namespace Klowee.Api.Tests;

/// <summary>Pure arithmetic: no database, no HTTP, no factory.</summary>
public class PricingServiceTests
{
    private readonly PricingService _pricing = new();

    private static PricingLine Line(decimal price, int quantity = 1, params decimal[] addOns) => new(price, quantity, addOns);

    [Fact]
    public void LineTotal_IsUnitPricePlusAddOns_TimesQuantity()
    {
        var result = _pricing.Price([Line(160m, 2, 30m, 25m), Line(110m)], DiscountType.None, 0m);

        Assert.Equal([430m, 110m], result.LineTotals);
        Assert.Equal(540m, result.Subtotal);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(540m, result.Total);
    }

    [Fact]
    public void Percent_RoundsHalfUpToTheCentavo()
    {
        // 435 × 12.5% = 54.375 → 54.38 (banker's rounding would give 54.38 too
        // here, so check one that differs: 145 × 12.5% = 18.125 → 18.13, not 18.12).
        Assert.Equal(54.38m, _pricing.Price([Line(145m, 3)], DiscountType.Percent, 12.5m).DiscountAmount);
        Assert.Equal(18.13m, _pricing.Price([Line(145m)], DiscountType.Percent, 12.5m).DiscountAmount);
        Assert.Equal(126.87m, _pricing.Price([Line(145m)], DiscountType.Percent, 12.5m).Total);
    }

    [Theory]
    [InlineData(0, 0, 200)]
    [InlineData(100, 200, 0)]
    public void Percent_AtTheEnds(decimal percent, decimal discount, decimal total)
    {
        var result = _pricing.Price([Line(200m)], DiscountType.Percent, percent);

        Assert.Equal(discount, result.DiscountAmount);
        Assert.Equal(total, result.Total);
    }

    [Fact]
    public void Fixed_IsTakenOffAsIs()
    {
        var result = _pricing.Price([Line(160m), Line(110m)], DiscountType.Fixed, 50m);

        Assert.Equal(50m, result.DiscountAmount);
        Assert.Equal(220m, result.Total);
    }

    [Fact]
    public void Fixed_IsCappedAtTheSubtotal_SoTheTotalNeverGoesNegative()
    {
        var result = _pricing.Price([Line(110m)], DiscountType.Fixed, 500m);

        Assert.Equal(110m, result.DiscountAmount);
        Assert.Equal(0m, result.Total);
    }

    [Fact]
    public void LoyaltyFree_FreesOneUnitOfTheCheapestLine_AddOnsIncluded()
    {
        // Per unit: 160, 110 + 30 = 140, 150. The 140 line is the cheapest.
        var result = _pricing.Price([Line(160m), Line(110m, 2, 30m), Line(150m)], DiscountType.LoyaltyFree, 0m);

        Assert.Equal(1, result.FreeLineIndex);
        Assert.Equal(140m, result.DiscountAmount);           // one unit, not the line's 280
        Assert.Equal(160m + 280m + 150m - 140m, result.Total);
    }

    [Fact]
    public void LoyaltyFree_OnATie_PicksTheFirstLine()
    {
        var result = _pricing.Price([Line(150m), Line(120m), Line(120m)], DiscountType.LoyaltyFree, 0m);

        Assert.Equal(1, result.FreeLineIndex);
    }

    [Fact]
    public void LoyaltyFree_IgnoresTheValue()
    {
        var result = _pricing.Price([Line(150m)], DiscountType.LoyaltyFree, 999m);

        Assert.Equal(150m, result.DiscountAmount);
        Assert.Equal(0m, result.Total);
    }

    [Fact]
    public void None_IgnoresTheValue() =>
        Assert.Equal(150m, _pricing.Price([Line(150m)], DiscountType.None, 50m).Total);
}
