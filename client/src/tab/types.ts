/**
 * The shape of `GET /api/public/tab/{token}` (handover 17), mirroring the
 * Public* records in `api/Klowee.Api/Contracts/Tabs/TabContracts.cs`.
 * `DateOnly` arrives as "2026-11-13"; `decimal` as a JSON number; enums as strings.
 */

export type PaymentChannel = 'Cash' | 'GCash' | 'BusinessGCash' | 'CIMB' | 'BPI' | 'Maya' | 'Other'
export type ClaimStatus = 'Pending' | 'Confirmed' | 'Rejected'
export type ClubStyle = 'Coffee' | 'Matcha'
export type RewardType = 'PercentOff' | 'FreeAddOn' | 'FreeDrink'
/** The card behind this tab: a member's live card, asked for, or none yet. */
export type Membership = 'member' | 'requested' | 'none'

export interface TabOrder {
  orderedOn: string
  items: string
  total: number
  paid: number
  due: number
}

export interface TabPayment {
  paidOn: string
  channel: PaymentChannel
  amount: number
}

export interface TabClaim {
  id: string
  amount: number
  reference: string
  status: ClaimStatus
  createdOn: string
  channel: PaymentChannel
  rejectReason: string | null
}

export interface TabReward {
  slot: number
  label: string
  type: RewardType
  /** "locked", "available" or "used". */
  state: string
  cycle: number
}

export interface TabSlot {
  slot: number
  filled: boolean
  date: string | null
  source: string | null
}

export interface TabCard {
  cardNumber: number
  isCurrent: boolean
  slots: TabSlot[]
  rewards: TabReward[]
}

export interface TabClub {
  clubId: string
  name: string
  style: ClubStyle
  slotCount: number
  membership: Membership
  requestedOn: string | null
  /** The club whose card brings this one; it has no Ask button of its own. */
  comesWithClubId: string | null
  comesWithName: string | null
  position: number
  cycle: number
  rewards: TabReward[]
  pendingDrinks: number
  cards: TabCard[]
  /** The index in `name` of the letter printed in pink (handover 17a); null or absent for none. */
  accentLetter?: number | null
}

export interface TabPayMethod {
  id: string
  channel: PaymentChannel
  label: string
  accountName: string
  accountNumber: string | null
  qrUrl: string | null
}

export interface PublicTab {
  name: string
  balance: number
  pendingClaimsTotal: number
  unpaidOrders: TabOrder[]
  recentPayments: TabPayment[]
  claims: TabClaim[]
  clubs: TabClub[]
  pay: { note: string | null; methods: TabPayMethod[] }
}

export interface ClaimRequest {
  amount: number
  reference: string
  paymentMethodId: string
}

/** How a channel reads to a customer: the business account is still "GCash" to them. */
export const CHANNEL_NAMES: Record<PaymentChannel, string> = {
  Cash: 'cash',
  GCash: 'GCash',
  BusinessGCash: 'GCash',
  CIMB: 'CIMB',
  BPI: 'BPI',
  Maya: 'Maya',
  Other: 'transfer',
}
