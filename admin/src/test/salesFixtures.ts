import type {
  AddOnDto,
  CustomerDetailDto,
  CustomerSummaryDto,
  MenuVersionDetailDto,
  MonthlyRowDto,
  OrderDetailDto,
  OutstandingRowDto,
  SummaryReportDto,
} from '../types/api'

export const kee: CustomerSummaryDto = {
  id: 'c0000000-0000-0000-0000-000000000001',
  name: 'Kee',
  nickname: null,
  notes: null,
  isActive: true,
  outstandingBalance: 320,
  loyaltyBalance: 4,
  availableRewards: 0,
  lastOrderOn: '2026-11-02',
}

export const prince: CustomerSummaryDto = {
  id: 'c0000000-0000-0000-0000-000000000002',
  name: 'Prince',
  nickname: 'P',
  notes: null,
  isActive: true,
  outstandingBalance: 0,
  loyaltyBalance: 12,
  availableRewards: 1,
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
    { id: 'vi000000-0000-0000-0000-000000000001', menuItemId: 'm1', name: 'Americano', categoryName: 'Coffee', price: 110, isAvailable: true, isFeatured: false, sortOrder: 0 },
    { id: 'vi000000-0000-0000-0000-000000000002', menuItemId: 'm2', name: 'Spanish Latte', categoryName: 'Coffee', price: 150, isAvailable: true, isFeatured: true, sortOrder: 1 },
    { id: 'vi000000-0000-0000-0000-000000000003', menuItemId: 'm3', name: 'Strawberry Milk', categoryName: 'Non-Coffee', price: 140, isAvailable: true, isFeatured: false, sortOrder: 2 },
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
