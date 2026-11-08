import { price, type PricingResult } from '../../../lib/pricing'
import type { AddOnDto, DiscountType, MenuVersionDetailDto, OrderDetailDto, OrderRequest } from '../../../types/api'

/** One drink the form can sell: from the current office menu, or already on the order being edited. */
export interface PickerItem {
  menuVersionItemId: string
  name: string
  category: string
  /** What the preview charges: today's menu price, or the order's snapshot for a line it already has. */
  unitPrice: number
}

export interface DraftLine {
  quantity: number
  addOnIds: string[]
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

export function draftFromOrder(order: OrderDetailDto): OrderDraft {
  return {
    customerId: order.customer.id,
    payerId: order.payer?.id ?? null,
    orderedOn: order.orderedOn,
    notes: order.notes ?? '',
    discountType: order.discountType,
    discountValue: order.discountType === 'Percent' || order.discountType === 'Fixed' ? String(order.discountValue) : '',
    lines: Object.fromEntries(
      order.items.map((item) => [item.menuVersionItemId, { quantity: item.quantity, addOnIds: item.addOns.map((a) => a.addOnId) }]),
    ),
  }
}

/**
 * What the picker lists: the office menu's available items, in its order, plus
 * any line of the edited order that isn't on it (an older menu), so editing
 * never silently drops a drink. Existing lines preview at their snapshot price,
 * because the API keeps it (decision 016).
 */
export function pickerItems(menu: MenuVersionDetailDto | undefined, order: OrderDetailDto | undefined): PickerItem[] {
  const snapshot = new Map(order?.items.map((item) => [item.menuVersionItemId, item]) ?? [])
  const fromMenu = (menu?.items ?? [])
    .filter((item) => item.isAvailable || snapshot.has(item.id))
    .map((item) => ({
      menuVersionItemId: item.id,
      name: item.name,
      category: item.categoryName,
      unitPrice: snapshot.get(item.id)?.unitPrice ?? item.price,
    }))
  const onMenu = new Set(fromMenu.map((item) => item.menuVersionItemId))
  const older = (order?.items ?? [])
    .filter((item) => !onMenu.has(item.menuVersionItemId))
    .map((item) => ({ menuVersionItemId: item.menuVersionItemId, name: item.name, category: 'From an earlier menu', unitPrice: item.unitPrice }))
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
): PricingResult {
  const addOnPrice = (menuVersionItemId: string, addOnId: string) =>
    order?.items.find((i) => i.menuVersionItemId === menuVersionItemId)?.addOns.find((a) => a.addOnId === addOnId)?.unitPrice ??
    addOns.find((a) => a.id === addOnId)?.price ??
    0
  return price(
    lines.map(({ item, line }) => ({
      unitPrice: item.unitPrice,
      quantity: line.quantity,
      addOnPrices: line.addOnIds.map((id) => addOnPrice(item.menuVersionItemId, id)),
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
    items: lines.map(({ item, line }) => ({ menuVersionItemId: item.menuVersionItemId, quantity: line.quantity, addOnIds: line.addOnIds })),
  }
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
