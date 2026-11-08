import type { DiscountType } from '../types/api'

/**
 * A preview of the API's PricingService, for the order form's live summary
 * only. The API recomputes and stores the real totals on save, and its answer
 * replaces this one (decision 017). The rules must match
 * api/Klowee.Api/Services/PricingService.cs; the tests are the same cases.
 */

export interface PricingLine {
  unitPrice: number
  quantity: number
  addOnPrices: number[]
}

export interface PricingResult {
  lineTotals: number[]
  subtotal: number
  discountAmount: number
  total: number
  /** The line one unit of which a free-drink reward covers. */
  freeLineIndex: number | null
}

// Work in whole centavos so half-up rounding is exact: 54.375 in floating
// point is 54.37499…, and rounding that gives the wrong centavo.
const toCents = (pesos: number) => Math.round(pesos * 100)
const toPesos = (cents: number) => cents / 100

export function price(lines: PricingLine[], discountType: DiscountType, discountValue: number): PricingResult {
  const unitCents = lines.map((line) => toCents(line.unitPrice) + line.addOnPrices.reduce((sum, p) => sum + toCents(p), 0))
  const lineCents = lines.map((line, index) => unitCents[index]! * line.quantity)
  const subtotal = lineCents.reduce((sum, cents) => sum + cents, 0)

  let freeLineIndex: number | null = null
  let discount = 0
  switch (discountType) {
    case 'Percent':
      // Half-up to the centavo; amounts are never negative, so Math.round is half-up.
      discount = Math.round((subtotal * discountValue) / 100)
      break
    case 'Fixed':
      discount = toCents(discountValue)
      break
    case 'LoyaltyFree':
      unitCents.forEach((cents, index) => {
        if (freeLineIndex === null || cents < unitCents[freeLineIndex]!) freeLineIndex = index
      })
      discount = freeLineIndex === null ? 0 : unitCents[freeLineIndex]!
      break
    default:
      discount = 0
  }

  discount = Math.min(Math.max(discount, 0), subtotal)
  return {
    lineTotals: lineCents.map(toPesos),
    subtotal: toPesos(subtotal),
    discountAmount: toPesos(discount),
    total: toPesos(subtotal - discount),
    freeLineIndex,
  }
}
