import type {
  AddOnDto,
  CustomerClubDto,
  CustomerDetailDto,
  CustomerRewardDto,
  CustomerSummaryDto,
  LoyaltyCardDto,
  LoyaltyClubDto,
  LoyaltySlotDto,
  MenuVersionDetailDto,
  MonthlyRowDto,
  OrderDetailDto,
  OutstandingRowDto,
  SummaryReportDto,
} from '../types/api'

export const extraShot: AddOnDto = { id: 'a0000000-0000-0000-0000-000000000002', name: 'Extra shot', price: 25, isActive: true }

/** The Coffee Club as the migration creates it: 12 slots, 50% off on 5, extra shot on 8, free drink on 12. */
export const coffeeClub: LoyaltyClubDto = {
  id: 'c0ffee00-0000-4000-8000-000000000001',
  name: 'The Coffee Club',
  style: 'Coffee',
  slotCount: 12,
  isActive: true,
  activatedOn: null,
  sortOrder: 1,
  autoJoinFromClubId: null,
  rewards: [
    { id: 'r-half', slot: 5, type: 'PercentOff', percent: 50, addOnId: null, addOnName: null, label: '50% OFF' },
    { id: 'r-shot', slot: 8, type: 'FreeAddOn', percent: null, addOnId: 'a0000000-0000-0000-0000-000000000002', addOnName: 'Extra shot', label: 'EXTRA SHOT' },
    { id: 'r-free', slot: 12, type: 'FreeDrink', percent: null, addOnId: null, addOnName: null, label: 'FREE DRINK' },
  ],
  categoryIds: ['cat-coffee'],
  memberCount: 2,
}

export const matchaClub: LoyaltyClubDto = {
  ...coffeeClub,
  id: '3a7c4a00-0000-4000-8000-000000000002',
  name: 'The Matcha Club',
  style: 'Matcha',
  isActive: false,
  sortOrder: 2,
  autoJoinFromClubId: 'c0ffee00-0000-4000-8000-000000000001',
  categoryIds: [],
  memberCount: 0,
}

/**
 * The card stack for a customer on card `cycle + 1` at `position`: earlier cards
 * full, slots stamped by `stamp(overall slot)` (undated by hand unless given).
 */
export const stackOf = (
  cycle: number,
  position: number,
  stamp: (overall: number) => Pick<LoyaltySlotDto, 'date' | 'orderId' | 'source'> = () => ({ date: null, orderId: null, source: 'adjustment' }),
  rewardsOf: (cardIndex: number) => CustomerRewardDto[] = () => [],
): LoyaltyCardDto[] =>
  Array.from({ length: cycle + 1 }, (_, card) => ({
    cardNumber: card + 1,
    isCurrent: card === cycle,
    slots: Array.from({ length: 12 }, (_, i) => {
      const overall = card * 12 + i + 1
      const filled = card < cycle || i < position
      return filled
        ? { slot: i + 1, filled: true, ...stamp(overall) }
        : { slot: i + 1, filled: false, date: null, orderId: null, source: null }
    }),
    rewards: rewardsOf(card),
  }))

/** A customer's Coffee Club card with `position` stamps on card 1, and each reward's state. */
export const coffeeCard = (position: number, states: CustomerRewardDto['state'][], pendingDrinks = 0): CustomerClubDto => ({
  clubId: coffeeClub.id,
  name: coffeeClub.name,
  style: 'Coffee',
  membership: 'member',
  membershipId: 'mem-coffee',
  joinedOn: null,
  slotCount: 12,
  position,
  cycle: 0,
  rewards: coffeeClub.rewards.map((r, i) => ({
    rewardId: r.id,
    slot: r.slot,
    label: r.label,
    type: r.type,
    state: states[i]!,
    cycle: 0,
    redemptionId: states[i] === 'used' ? `red-${r.id}` : null,
    orderId: null,
  })),
  pendingDrinks,
  cards: stackOf(0, position, undefined, () =>
    coffeeClub.rewards.map((r, i) => ({
      rewardId: r.id, slot: r.slot, label: r.label, type: r.type, state: states[i]!, cycle: 0,
      redemptionId: states[i] === 'used' ? `red-${r.id}` : null, orderId: null,
    })),
  ),
})

export const kee: CustomerSummaryDto = {
  id: 'c0000000-0000-0000-0000-000000000001',
  name: 'Kee',
  nickname: null,
  notes: null,
  isActive: true,
  outstandingBalance: 320,
  // Four stamps: the 50% off on slot 5 is ready.
  clubs: [coffeeCard(4, ['available', 'locked', 'locked'], 2)],
  lastOrderOn: '2026-11-02',
}

export const prince: CustomerSummaryDto = {
  id: 'c0000000-0000-0000-0000-000000000002',
  name: 'Prince',
  nickname: 'P',
  notes: null,
  isActive: true,
  outstandingBalance: 0,
  // Eleven stamps, 50% off used: the extra shot and the free drink are ready.
  clubs: [coffeeCard(11, ['used', 'available', 'available'])],
  lastOrderOn: '2026-11-07',
}

export const keeDetail: CustomerDetailDto = { ...kee, recentOrders: [] }

export const oatMilk: AddOnDto = { id: 'a0000000-0000-0000-0000-000000000001', name: 'Oat milk', price: 30, isActive: true }

export const officeMenu: MenuVersionDetailDto = {
  id: 'v0000000-0000-0000-0000-00000000000f',
  name: 'Office menu Nov 2026',
  context: 'Office',
  effectiveFrom: '2026-11-01',
  isPublished: true,
  notes: null,
  items: [
    { id: 'vi000000-0000-0000-0000-000000000001', menuItemId: 'm1', name: 'Americano', categoryName: 'Coffee', price: 110, isAvailable: true, isFeatured: false, sortOrder: 0, loyaltyClubId: coffeeClub.id },
    { id: 'vi000000-0000-0000-0000-000000000002', menuItemId: 'm2', name: 'Spanish Latte', categoryName: 'Coffee', price: 150, isAvailable: true, isFeatured: true, sortOrder: 1, loyaltyClubId: coffeeClub.id },
    { id: 'vi000000-0000-0000-0000-000000000003', menuItemId: 'm3', name: 'Strawberry Milk', categoryName: 'Non-Coffee', price: 140, isAvailable: true, isFeatured: false, sortOrder: 2, loyaltyClubId: null },
  ],
} as MenuVersionDetailDto

export const unpaidOrder: OrderDetailDto = {
  id: 'o0000000-0000-0000-0000-000000000001',
  context: 'Office',
  orderedOn: '2026-11-08',
  customer: { id: kee.id, name: kee.name },
  payer: null,
  notes: null,
  discountType: 'None',
  discountValue: 0,
  subtotal: 190,
  discountAmount: 0,
  total: 190,
  paid: 0,
  balance: 190,
  status: 'Unpaid',
  items: [
    {
      id: 'oi000000-0000-0000-0000-000000000001',
      menuVersionItemId: officeMenu.items[0]!.id,
      menuItemId: 'm1',
      name: 'Americano',
      menuVersionName: officeMenu.name,
      quantity: 1,
      unitPrice: 160,
      lineTotal: 190,
      addOns: [{ id: 'oa1', addOnId: oatMilk.id, name: 'Oat milk', unitPrice: 30 }],
      reward: null,
    },
  ],
  payments: [],
  warnings: [],
}

export const summary: SummaryReportDto = {
  from: '2026-11-01',
  to: '2026-11-30',
  revenue: 1250,
  collected: 940,
  outstanding: 610,
  orderCount: 7,
  drinkCount: 9,
  byChannel: [{ channel: 'GCash', amount: 940, count: 4 }],
  topItems: [
    { menuItemId: 'm2', name: 'Spanish Latte', quantity: 5 },
    { menuItemId: 'm1', name: 'Americano', quantity: 4 },
  ],
  newCustomers: 2,
}

export const outstanding: OutstandingRowDto[] = [
  { customerId: kee.id, name: 'Kee', balance: 320, oldestUnpaidOn: '2026-10-27', orderCount: 2 },
]

/** One row per month from `from` to `to` ("2026-11"); history starts in May 2025, with ₱800 in Oct 2026 and ₱1,250 in Nov 2026. */
export const monthly = (from: string | null, to: string): MonthlyRowDto[] => {
  const [fy, fm] = (from ?? '2025-05').split('-').map(Number) as [number, number]
  const [ty, tm] = to.split('-').map(Number) as [number, number]
  return Array.from({ length: (ty - fy) * 12 + tm - fm + 1 }, (_, index) => {
    const year = fy + Math.floor((fm - 1 + index) / 12)
    const month = ((fm - 1 + index) % 12) + 1
    const amount = year === 2026 && month === 11 ? 1250 : year === 2026 && month === 10 ? 800 : 0
    return { year, month, revenue: amount, collected: amount === 1250 ? 940 : amount, orders: 0, drinks: 0 }
  })
}
