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
})
