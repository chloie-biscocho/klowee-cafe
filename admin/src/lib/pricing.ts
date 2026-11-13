import type { DiscountType, LoyaltyRewardType } from '../types/api'

/**
 * A preview of the API's PricingService, for the order form's live summary
 * only. Line rewards (decision 020) are priced before the order discount. The API recomputes and stores the real totals on save, and its answer
 * replaces this one (decision 017). The rules must match
 * api/Klowee.Api/Services/PricingService.cs; the tests are the same cases.
 */

/**
 * A loyalty reward on one unit of a line. `percent` is for PercentOff;
 * `addOnPriceOnLine` is for FreeAddOn: the add-on's price when the line already
 * carries it (one unit's copy is then free), or null when it is added at ₱0.
 */
export interface PricingReward {
  type: LoyaltyRewardType
  percent?: number
  addOnPriceOnLine?: number | null
}

export interface PricingLine {
  unitPrice: number
  quantity: number
  addOnPrices: number[]
  reward?: PricingReward | null
}

export interface PricingResult {
  /** Per line, after its reward. */
  lineTotals: number[]
  /** Per line, what its reward took off. */
  rewardAmounts: number[]
  /** The line totals added up: after rewards, before the order discount. */
  subtotal: number
  discountAmount: number
  total: number
  /** The line one unit of which an old LoyaltyFree discount covers (history only). */
  freeLineIndex: number | null
}

// Work in whole centavos so half-up rounding is exact: 54.375 in floating
// point is 54.37499…, and rounding that gives the wrong centavo.
const toCents = (pesos: number) => Math.round(pesos * 100)
const toPesos = (cents: number) => cents / 100

/** What a line's reward takes off one unit, in centavos, before capping at the line. */
function rewardCents(line: PricingLine): number {
  const reward = line.reward
  if (!reward) return 0
  switch (reward.type) {
    case 'PercentOff':
      // Half-up to the centavo, on the drink price only (add-ons charged in full).
      return Math.round((toCents(line.unitPrice) * (reward.percent ?? 0)) / 100)
    case 'FreeAddOn':
      return reward.addOnPriceOnLine == null ? 0 : toCents(reward.addOnPriceOnLine)
    case 'FreeDrink':
      // The drink only: add-ons are still charged.
      return toCents(line.unitPrice)
  }
}

export function price(lines: PricingLine[], discountType: DiscountType, discountValue: number): PricingResult {
  const unitCents = lines.map((line) => toCents(line.unitPrice) + line.addOnPrices.reduce((sum, p) => sum + toCents(p), 0))
  const grossCents = lines.map((line, index) => unitCents[index]! * line.quantity)
  // Line rewards come first, each capped at its line; the order discount applies to what is left.
  const rewards = lines.map((line, index) => Math.min(Math.max(rewardCents(line), 0), grossCents[index]!))
  const lineCents = grossCents.map((cents, index) => cents - rewards[index]!)
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
    rewardAmounts: rewards.map(toPesos),
    subtotal: toPesos(subtotal),
    discountAmount: toPesos(discount),
    total: toPesos(subtotal - discount),
    freeLineIndex,
  }
}
