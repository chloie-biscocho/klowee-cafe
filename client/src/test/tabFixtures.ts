import type { PublicTab, TabCard, TabClub, TabPayMethod, TabReward } from '../tab/types'

export const TOKEN = 'AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_AbCdE'
export const TAB_URL = `http://localhost:5181/api/public/tab/${TOKEN}`

export const gcash: TabPayMethod = {
  id: 'pm-gcash', channel: 'BusinessGCash', label: 'GCash', accountName: 'Klowee Cafe', accountNumber: '0917 123 4567',
  qrUrl: 'https://fake.supabase.test/storage/v1/object/public/media/site/gcash-qr.png',
}
export const cimb: TabPayMethod = {
  id: 'pm-cimb', channel: 'CIMB', label: 'CIMB Bank', accountName: 'Chloie B.', accountNumber: '8001 2345 67',
  qrUrl: 'https://fake.supabase.test/storage/v1/object/public/media/site/cimb-qr.png',
}

const coffeeRewards = (states: string[], cycle = 0): TabReward[] => [
  { slot: 5, label: '50% OFF', type: 'PercentOff', state: states[0]!, cycle },
  { slot: 8, label: 'EXTRA SHOT', type: 'FreeAddOn', state: states[1]!, cycle },
  { slot: 12, label: 'FREE DRINK', type: 'FreeDrink', state: states[2]!, cycle },
]

/** A card with its first `filled` slots stamped, dated Nov 1, 2, 3… */
export function card(cardNumber: number, filled: number, states: string[], isCurrent: boolean): TabCard {
  return {
    cardNumber,
    isCurrent,
    slots: Array.from({ length: 12 }, (_, i) => ({
      slot: i + 1,
      filled: i < filled,
      date: i < filled ? `2026-10-${String(i + 10).padStart(2, '0')}` : null,
      source: i < filled ? 'order' : null,
    })),
    rewards: coffeeRewards(states, cardNumber - 1),
  }
}

export const coffeeMember: TabClub = {
  clubId: 'club-coffee', name: 'The Coffee Club', style: 'Coffee', slotCount: 12, membership: 'member', requestedOn: null,
  comesWithClubId: null, comesWithName: null, position: 6, cycle: 0, rewards: coffeeRewards(['used', 'locked', 'locked']),
  pendingDrinks: 2, cards: [card(1, 6, ['used', 'locked', 'locked'], true)],
}

export const matchaNone: TabClub = {
  clubId: 'club-matcha', name: 'The Matcha Club', style: 'Matcha', slotCount: 12, membership: 'none', requestedOn: null,
  comesWithClubId: null, comesWithName: null, position: 0, cycle: 0,
  rewards: coffeeRewards(['locked', 'locked', 'locked']).map((r) => ({ ...r, label: r.slot === 8 ? 'FREE TOPPING' : r.label })),
  pendingDrinks: 0, cards: [],
}

export const tab: PublicTab = {
  name: 'Dean',
  balance: 780,
  pendingClaimsTotal: 0,
  unpaidOrders: [
    { orderedOn: '2026-10-21', items: 'Oat Biscoff Latte ×2', total: 380, paid: 200, due: 180 },
    { orderedOn: '2026-11-11', items: 'Strawberry Milk, Americano', total: 250, paid: 0, due: 250 },
    { orderedOn: '2026-11-12', items: 'Spanish Latte, Dirty Matcha', total: 350, paid: 0, due: 350 },
  ],
  recentPayments: [
    { paidOn: '2026-10-28', channel: 'GCash', amount: 200 },
    { paidOn: '2026-10-09', channel: 'Cash', amount: 460 },
  ],
  claims: [],
  clubs: [coffeeMember, matchaNone],
  pay: { note: null, methods: [gcash] },
}
