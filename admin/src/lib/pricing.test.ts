import { describe, expect, it } from 'vitest'
import { price, type PricingLine } from './pricing'

/** The same cases as api/Klowee.Api.Tests/PricingServiceTests.cs: same inputs, same numbers. */
const line = (unitPrice: number, quantity = 1, ...addOnPrices: number[]): PricingLine => ({
  unitPrice,
  quantity,
  addOnPrices,
})

describe('price (mirror of PricingService)', () => {
  it('line total is unit price plus add-ons, times quantity', () => {
    const result = price([line(160, 2, 30, 25), line(110)], 'None', 0)
    expect(result.lineTotals).toEqual([430, 110])
    expect(result.subtotal).toBe(540)
    expect(result.discountAmount).toBe(0)
    expect(result.total).toBe(540)
  })

  it('percent rounds half-up to the centavo', () => {
    expect(price([line(145, 3)], 'Percent', 12.5).discountAmount).toBe(54.38)
    expect(price([line(145)], 'Percent', 12.5).discountAmount).toBe(18.13)
    expect(price([line(145)], 'Percent', 12.5).total).toBe(126.87)
  })

  it.each([
    [0, 0, 200],
    [100, 200, 0],
  ])('percent %s%% at the ends', (percent, discount, total) => {
    const result = price([line(200)], 'Percent', percent)
    expect(result.discountAmount).toBe(discount)
    expect(result.total).toBe(total)
  })

  it('fixed is taken off as is', () => {
    const result = price([line(160), line(110)], 'Fixed', 50)
    expect(result.discountAmount).toBe(50)
    expect(result.total).toBe(220)
  })

  it('fixed is capped at the subtotal, so the total never goes negative', () => {
    const result = price([line(110)], 'Fixed', 500)
    expect(result.discountAmount).toBe(110)
    expect(result.total).toBe(0)
  })

  it('a free drink frees one unit of the cheapest line, add-ons included', () => {
    const result = price([line(160), line(110, 2, 30), line(150)], 'LoyaltyFree', 0)
    expect(result.freeLineIndex).toBe(1)
    expect(result.discountAmount).toBe(140)
    expect(result.total).toBe(160 + 280 + 150 - 140)
  })

  it('a free drink on a tie picks the first line', () => {
    expect(price([line(150), line(120), line(120)], 'LoyaltyFree', 0).freeLineIndex).toBe(1)
  })

  it('a free drink ignores the value', () => {
    const result = price([line(150)], 'LoyaltyFree', 999)
    expect(result.discountAmount).toBe(150)
    expect(result.total).toBe(0)
  })

  it('none ignores the value', () => {
    expect(price([line(150)], 'None', 50).total).toBe(150)
  })

  // ---- Line rewards: the same cases as PricingServiceTests, one for one ----

  const halfOff = { type: 'PercentOff' as const, percent: 50 }
  const freeDrink = { type: 'FreeDrink' as const }
  const reward = (r: PricingLine['reward'], unitPrice: number, quantity = 1, ...addOnPrices: number[]): PricingLine => ({
    ...line(unitPrice, quantity, ...addOnPrices),
    reward: r,
  })

  it('percent off takes the percent off one unit’s drink price', () => {
    const result = price([reward(halfOff, 160)], 'None', 0)
    expect(result.lineTotals).toEqual([80])
    expect(result.rewardAmounts).toEqual([80])
    expect(result.total).toBe(80)
  })

  it('percent off charges add-ons in full', () => {
    expect(price([reward(halfOff, 160, 1, 30)], 'None', 0).total).toBe(110)
  })

  it('percent off on quantity two affects one unit only', () => {
    expect(price([reward(halfOff, 160, 2)], 'None', 0).total).toBe(240)
  })

  it('percent off rounds half-up to the centavo', () => {
    const result = price([reward({ type: 'PercentOff', percent: 12.5 }, 145)], 'None', 0)
    expect(result.rewardAmounts).toEqual([18.13])
    expect(result.total).toBe(126.87)
  })

  it('percent off comes before an order percent', () => {
    const result = price([reward(halfOff, 160, 1, 30), line(110)], 'Percent', 10)
    expect(result.subtotal).toBe(220)
    expect(result.discountAmount).toBe(22)
    expect(result.total).toBe(198)
  })

  it('a free add-on on the line makes one unit’s copy free', () => {
    const shot = { type: 'FreeAddOn' as const, addOnPriceOnLine: 25 }
    expect(price([reward(shot, 160, 1, 25)], 'None', 0).total).toBe(160)
    expect(price([reward(shot, 160, 2, 25)], 'None', 0).total).toBe(345)
  })

  it('a free add-on not on the line is given at zero', () => {
    const result = price([reward({ type: 'FreeAddOn' }, 160)], 'None', 0)
    expect(result.rewardAmounts).toEqual([0])
    expect(result.total).toBe(160)
  })

  it('a free drink makes one unit’s drink free', () => {
    expect(price([reward(freeDrink, 110)], 'None', 0).total).toBe(0)
  })

  it('a free drink still charges the add-ons', () => {
    expect(price([reward(freeDrink, 160, 1, 30)], 'None', 0).total).toBe(30)
  })

  it('a free drink on quantity two frees one unit’s drink only', () => {
    expect(price([reward(freeDrink, 160, 2, 30)], 'None', 0).total).toBe(220)
  })

  it('a free drink comes before an order percent', () => {
    const result = price([reward(freeDrink, 160, 1, 30), line(110)], 'Percent', 10)
    expect(result.lineTotals).toEqual([30, 110])
    expect(result.discountAmount).toBe(14)
    expect(result.total).toBe(126)
  })

  it('a fixed discount is capped at the subtotal after rewards', () => {
    const result = price([reward(freeDrink, 110)], 'Fixed', 50)
    expect(result.subtotal).toBe(0)
    expect(result.discountAmount).toBe(0)
    expect(result.total).toBe(0)
  })
})
