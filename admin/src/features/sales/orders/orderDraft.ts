import { availableRewards, clubLabel, rewardPhrase } from '../../../lib/loyalty'
import { price, type PricingReward, type PricingResult } from '../../../lib/pricing'
import type {
  AddOnDto,
  CustomerSummaryDto,
  DiscountType,
  LoyaltyClubDto,
  MenuVersionDetailDto,
  OrderDetailDto,
  OrderRequest,
} from '../../../types/api'

/** One drink the form can sell: from the current office menu, or already on the order being edited. */
export interface PickerItem {
  menuVersionItemId: string
  name: string
  category: string
  /** What the preview charges: today's menu price, or the order's snapshot for a line it already has. */
  unitPrice: number
  /** The loyalty card this drink stamps, or null: which rewards it can carry. */
  loyaltyClubId: string | null
}

export interface DraftLine {
  quantity: number
  addOnIds: string[]
  /** A loyalty reward on one unit of the line (decision 020). */
  rewardId?: string | null
}

/** The order form's local state. `lines` is keyed by menu version item; quantity 0 is "not ordered". */
export interface OrderDraft {
  customerId: string | null
  payerId: string | null
  orderedOn: string
  notes: string
  discountType: DiscountType
  discountValue: string
  lines: Record<string, DraftLine>
}

export const emptyDraft = (today: string): OrderDraft => ({
  customerId: null,
  payerId: null,
  orderedOn: today,
  notes: '',
  discountType: 'None',
  discountValue: '',
  lines: {},
})

/**
 * The form's starting point for an existing order. The old single card's free
 * drink (LoyaltyFree) is history only and can't be saved again, so it starts
 * as no discount; the form says so.
 */
export function draftFromOrder(order: OrderDetailDto): OrderDraft {
  return {
    customerId: order.customer.id,
    payerId: order.payer?.id ?? null,
    orderedOn: order.orderedOn,
    notes: order.notes ?? '',
    discountType: order.discountType === 'LoyaltyFree' ? 'None' : order.discountType,
    discountValue: order.discountType === 'Percent' || order.discountType === 'Fixed' ? String(order.discountValue) : '',
    lines: Object.fromEntries(
      order.items.map((item) => [
        item.menuVersionItemId,
        { quantity: item.quantity, addOnIds: item.addOns.map((a) => a.addOnId), rewardId: item.reward?.rewardId ?? null },
      ]),
    ),
  }
}

/**
 * What the picker lists: the office menu's available items, in its order, plus
 * any line of the edited order that isn't on it (an older menu), so editing
 * never silently drops a drink. Existing lines preview at their snapshot price,
 * because the API keeps it (decision 016).
 */
export function pickerItems(
  menu: MenuVersionDetailDto | undefined,
  order: OrderDetailDto | undefined,
  clubs: LoyaltyClubDto[] = [],
): PickerItem[] {
  const clubOfReward = (rewardId: string | undefined) => clubs.find((c) => c.rewards.some((r) => r.id === rewardId))?.id ?? null
  const snapshot = new Map(order?.items.map((item) => [item.menuVersionItemId, item]) ?? [])
  const fromMenu = (menu?.items ?? [])
    .filter((item) => item.isAvailable || snapshot.has(item.id))
    .map((item) => ({
      menuVersionItemId: item.id,
      name: item.name,
      category: item.categoryName,
      unitPrice: snapshot.get(item.id)?.unitPrice ?? item.price,
      loyaltyClubId: item.loyaltyClubId,
    }))
  const onMenu = new Set(fromMenu.map((item) => item.menuVersionItemId))
  const older = (order?.items ?? [])
    .filter((item) => !onMenu.has(item.menuVersionItemId))
    .map((item) => ({
      menuVersionItemId: item.menuVersionItemId,
      name: item.name,
      category: 'From an earlier menu',
      unitPrice: item.unitPrice,
      // Not on today's menu, so its card is only known through the reward it already carries.
      loyaltyClubId: clubOfReward(item.reward?.rewardId),
    }))
  return [...fromMenu, ...older]
}

export interface PreviewLine {
  item: PickerItem
  line: DraftLine
}

/** The ordered lines, in picker order, with what the preview charges for each add-on. */
export function previewLines(draft: OrderDraft, items: PickerItem[]): PreviewLine[] {
  return items
    .filter((item) => (draft.lines[item.menuVersionItemId]?.quantity ?? 0) > 0)
    .map((item) => ({ item, line: draft.lines[item.menuVersionItemId]! }))
}

export function preview(
  lines: PreviewLine[],
  draft: OrderDraft,
  addOns: AddOnDto[],
  order: OrderDetailDto | undefined,
  clubs: LoyaltyClubDto[] = [],
): PricingResult {
  const addOnPrice = (menuVersionItemId: string, addOnId: string) =>
    order?.items.find((i) => i.menuVersionItemId === menuVersionItemId)?.addOns.find((a) => a.addOnId === addOnId)?.unitPrice ??
    addOns.find((a) => a.id === addOnId)?.price ??
    0
  const rewardFor = ({ item, line }: PreviewLine): PricingReward | null => {
    const reward = clubs.flatMap((c) => c.rewards).find((r) => r.id === line.rewardId)
    if (!reward) return null
    return {
      type: reward.type,
      percent: reward.percent ?? 0,
      addOnPriceOnLine:
        reward.type === 'FreeAddOn' && reward.addOnId && line.addOnIds.includes(reward.addOnId)
          ? addOnPrice(item.menuVersionItemId, reward.addOnId)
          : null,
    }
  }
  return price(
    lines.map((previewLine) => ({
      unitPrice: previewLine.item.unitPrice,
      quantity: previewLine.line.quantity,
      addOnPrices: previewLine.line.addOnIds.map((id) => addOnPrice(previewLine.item.menuVersionItemId, id)),
      reward: rewardFor(previewLine),
    })),
    draft.discountType,
    Number(draft.discountValue) || 0,
  )
}

export function toRequest(draft: OrderDraft, lines: PreviewLine[]): OrderRequest {
  return {
    customerId: draft.customerId!,
    payerId: draft.payerId,
    orderedOn: draft.orderedOn,
    notes: draft.notes.trim() === '' ? null : draft.notes.trim(),
    discountType: draft.discountType,
    discountValue: draft.discountType === 'Percent' || draft.discountType === 'Fixed' ? Number(draft.discountValue) || 0 : 0,
    items: lines.map(({ item, line }) => ({
      menuVersionItemId: item.menuVersionItemId,
      quantity: line.quantity,
      addOnIds: line.addOnIds,
      rewardId: line.rewardId ?? null,
    })),
  }
}

/** A reward toggle on a line: "50% off, Coffee Club". */
export interface RewardOption {
  rewardId: string
  label: string
  /** Off when every copy of this reward is already on another line. */
  enabled: boolean
}

/**
 * The rewards a line's drink can carry for this customer: those available on
 * the drink's card, counting each pass round the card separately, less the
 * ones other lines already carry. Rewards the order being edited already uses
 * count as available here: re-saving keeps them.
 */
export function rewardOptions(
  item: PickerItem,
  draft: OrderDraft,
  customer: CustomerSummaryDto | undefined,
  order: OrderDetailDto | undefined,
): RewardOption[] {
  const club = customer?.clubs.find((c) => c.clubId === item.loyaltyClubId && c.membership === 'member')
  if (!club) return []

  const ownOrder = order && order.customer.id === customer!.id ? order.id : null
  const usable = [...availableRewards(club), ...club.rewards.filter((r) => r.state === 'used' && r.orderId !== null && r.orderId === ownOrder)]
  const copies = new Map<string, { label: string; count: number }>()
  for (const reward of usable) {
    const entry = copies.get(reward.rewardId) ?? { label: reward.label, count: 0 }
    copies.set(reward.rewardId, { label: entry.label, count: entry.count + 1 })
  }

  const elsewhere = (rewardId: string) =>
    Object.entries(draft.lines).filter(
      ([id, line]) => id !== item.menuVersionItemId && line.quantity > 0 && line.rewardId === rewardId,
    ).length
  const mine = draft.lines[item.menuVersionItemId]?.rewardId

  return [...copies.entries()].map(([rewardId, { label, count }]) => ({
    rewardId,
    label: `${rewardPhrase(label)}, ${clubLabel(club.name)}`,
    enabled: mine === rewardId || count - elsewhere(rewardId) > 0,
  }))
}

/** Why the draft can't be saved yet, or null. The API still validates everything. */
export function blockingProblem(draft: OrderDraft, lines: PreviewLine[], subtotal: number): string | null {
  if (!draft.customerId) return 'Choose a customer.'
  if (lines.length === 0) return 'Add at least one drink.'
  const value = Number(draft.discountValue)
  if (draft.discountType === 'Percent' && !(value >= 0 && value <= 100)) return 'A percent discount is 0 to 100.'
  if (draft.discountType === 'Fixed' && !(value >= 0 && value <= subtotal)) return 'A fixed discount is 0 to the subtotal.'
  return null
}
