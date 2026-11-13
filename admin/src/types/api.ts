/**
 * Wire types for the Klowee Cafe API. These mirror the C# records in
 * `api/Klowee.Api/Contracts/` exactly — same names, same nullability — so a
 * change on either side shows up as a TypeScript error here rather than as a
 * runtime surprise.
 *
 * Notes on the mapping:
 *  - C# `DateOnly` arrives as an ISO date string, "2026-04-16".
 *  - C# `decimal` arrives as a JSON number.
 *  - Enums are serialised as strings (`JsonStringEnumConverter` in Program.cs).
 */

/** Declared as a tuple so it can drive both the union type and a zod enum. */
export const MENU_CONTEXTS = ['Office', 'PopUp'] as const

export type MenuContext = (typeof MENU_CONTEXTS)[number]

/** Human label for a context; the wire value is not something to show an owner. */
export const MENU_CONTEXT_LABELS: Record<MenuContext, string> = {
  Office: 'Office',
  PopUp: 'Pop-up',
}

// ---- Auth ----

export interface UserDto {
  id: string
  email: string
  displayName: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  /** ISO instant, e.g. "2026-04-16T09:30:00+00:00". */
  expiresAt: string
  user: UserDto
}

// ---- Menu categories ----

export interface MenuCategoryDto {
  id: string
  name: string
  sortOrder: number
  /** Optional line under the name on the public menu. */
  description: string | null
  /** The loyalty card this category's drinks stamp; null for none. */
  loyaltyClubId: string | null
}

export interface MenuCategoryRequest {
  name: string
  sortOrder: number
  description: string | null
  /** A PUT replaces every field: send the club back, or null for none. */
  loyaltyClubId: string | null
}

// ---- Menu items ----

export interface MenuItemDto {
  id: string
  name: string
  description: string
  categoryId: string
  categoryName: string
  photoUrl: string | null
  isArchived: boolean
  loyaltyMode: LoyaltyMode
  /** Set only when loyaltyMode is 'Club'. */
  loyaltyClubId: string | null
}

export interface MenuItemRequest {
  name: string
  description: string | null
  categoryId: string
  photoUrl: string | null
  loyaltyMode: LoyaltyMode
  loyaltyClubId: string | null
}

// ---- Add-ons ----

export interface AddOnDto {
  id: string
  name: string
  price: number
  isActive: boolean
}

export interface AddOnRequest {
  name: string
  price: number
  isActive: boolean
}

// ---- Menu versions ----

export interface MenuVersionSummaryDto {
  id: string
  name: string
  context: MenuContext
  effectiveFrom: string
  isPublished: boolean
  notes: string | null
  itemCount: number
}

export interface MenuVersionItemDto {
  id: string
  menuItemId: string
  name: string
  categoryName: string
  price: number
  isAvailable: boolean
  isFeatured: boolean
  sortOrder: number
  /** The loyalty card this drink stamps (item override, else its category's); null for none. */
  loyaltyClubId: string | null
}

export interface MenuVersionDetailDto {
  id: string
  name: string
  context: MenuContext
  effectiveFrom: string
  isPublished: boolean
  notes: string | null
  items: MenuVersionItemDto[]
}

export interface CreateMenuVersionRequest {
  name: string
  context: MenuContext
  effectiveFrom: string
  notes: string | null
  copyFromVersionId: string | null
}

/** A version's context is fixed at creation, so it is absent here. */
export interface UpdateMenuVersionRequest {
  name: string
  effectiveFrom: string
  notes: string | null
}

export interface MenuVersionItemRequest {
  menuItemId: string
  price: number
  isAvailable: boolean
  isFeatured: boolean
  sortOrder: number
}

// ---- Uploads ----

export type UploadFolder = 'menu' | 'events' | 'site'

export interface UploadResultDto {
  /** Public URL, safe to store on an entity and render on the site. */
  url: string
  path: string
  contentType: string
  size: number
}

// ---- Packages ----

export interface PackageInclusionDto {
  id: string
  text: string
  sortOrder: number
}

export interface PackageDto {
  id: string
  name: string
  price: number
  description: string
  guestCountNote: string
  isActive: boolean
  sortOrder: number
  inclusions: PackageInclusionDto[]
}

export interface PackageInclusionRequest {
  text: string
  sortOrder: number
}

/** The whole package; a PUT replaces the inclusion list in full. */
export interface PackageRequest {
  name: string
  price: number
  description: string | null
  guestCountNote: string | null
  isActive: boolean
  sortOrder: number
  inclusions: PackageInclusionRequest[]
}

// ---- Events ----

export const EVENT_STATUSES = ['Planned', 'Upcoming', 'Done', 'Cancelled'] as const

export type EventStatus = (typeof EVENT_STATUSES)[number]

export interface EventSummaryDto {
  id: string
  name: string
  venue: string
  startsOn: string
  endsOn: string
  status: EventStatus
  isPublished: boolean
  coverPhotoUrl: string | null
  photoCount: number
}

export interface EventPhotoDto {
  id: string
  url: string
  caption: string | null
  sortOrder: number
}

export interface EventDetailDto {
  id: string
  name: string
  venue: string
  address: string | null
  startsOn: string
  endsOn: string
  description: string | null
  coverPhotoUrl: string | null
  status: EventStatus
  isPublished: boolean
  photos: EventPhotoDto[]
}

export interface EventRequest {
  name: string
  venue: string
  address: string | null
  startsOn: string
  endsOn: string
  description: string | null
  coverPhotoUrl: string | null
  status: EventStatus
}

export interface EventPhotoRequest {
  url: string
  caption: string | null
  sortOrder: number
}

// ---- Announcements ----

export interface AnnouncementDto {
  id: string
  text: string
  linkUrl: string | null
  /** ISO instant. The API stores UTC; see `lib/dates.ts` for Manila display. */
  activeFrom: string
  /** `null` means open-ended. */
  activeUntil: string | null
  isActive: boolean
}

export interface AnnouncementRequest {
  text: string
  linkUrl: string | null
  /** ISO instant with an offset, e.g. "2026-05-21T20:00:00+08:00". */
  activeFrom: string
  activeUntil: string | null
  isActive: boolean
}

// ---- Site settings ----

/**
 * The closed key list from `SiteSettingService.KnownKeys`. The API answers 400
 * to anything else, so a typo here is a compile error rather than a dead row.
 */
export const SETTING_KEYS = [
  'hero_heading',
  'hero_body',
  'hero_image_url',
  'story_heading',
  'story_body',
  'story_image_url',
  'ticker_fallback',
  'book_body',
  'instagram_url',
  'facebook_url',
  'contact_email',
] as const

export type SettingKey = (typeof SETTING_KEYS)[number]

export interface SiteSettingDto {
  key: string
  value: string
}

export interface SiteSettingRequest {
  key: SettingKey
  value: string | null
}

// ---- Errors ----

/** RFC 7807, the single error shape the API returns (see decision 005). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  /** Present on the automatic 400 from `[ApiController]` model validation. */
  errors?: Record<string, string[]>
}

// ---- Sales (phase 2, decision 016) ----

export const PAYMENT_STATUSES = ['Unpaid', 'Partial', 'Paid'] as const
export type PaymentStatus = (typeof PAYMENT_STATUSES)[number]

export const PAYMENT_CHANNELS = ['Cash', 'GCash', 'BusinessGCash', 'CIMB', 'BPI', 'Maya', 'Other'] as const
export type PaymentChannel = (typeof PAYMENT_CHANNELS)[number]

export const PAYMENT_CHANNEL_LABELS: Record<PaymentChannel, string> = {
  Cash: 'Cash',
  GCash: 'GCash',
  BusinessGCash: 'Business GCash',
  CIMB: 'CIMB',
  BPI: 'BPI',
  Maya: 'Maya',
  Other: 'Other',
}

/** LoyaltyFree is the old single card's free drink: history only, refused on new and edited orders. */
export const DISCOUNT_TYPES = ['None', 'Percent', 'Fixed', 'LoyaltyFree'] as const
export type DiscountType = (typeof DISCOUNT_TYPES)[number]

// ---- Loyalty cards (decision 020) ----

export const LOYALTY_REWARD_TYPES = ['PercentOff', 'FreeAddOn', 'FreeDrink'] as const
export type LoyaltyRewardType = (typeof LOYALTY_REWARD_TYPES)[number]

export const LOYALTY_REWARD_TYPE_LABELS: Record<LoyaltyRewardType, string> = {
  PercentOff: 'Percent off the drink',
  FreeAddOn: 'Free add-on',
  FreeDrink: 'Free drink',
}

export const LOYALTY_CLUB_STYLES = ['Coffee', 'Matcha'] as const
export type LoyaltyClubStyle = (typeof LOYALTY_CLUB_STYLES)[number]

/** Which club an item stamps: its category's, none, or its own. */
export const LOYALTY_MODES = ['Inherit', 'None', 'Club'] as const
export type LoyaltyMode = (typeof LOYALTY_MODES)[number]

export interface LoyaltyClubRewardDto {
  id: string
  slot: number
  type: LoyaltyRewardType
  percent: number | null
  addOnId: string | null
  addOnName: string | null
  label: string
}

export interface LoyaltyClubDto {
  id: string
  name: string
  style: LoyaltyClubStyle
  slotCount: number
  isActive: boolean
  activatedOn: string | null
  sortOrder: number
  rewards: LoyaltyClubRewardDto[]
  /** The categories whose drinks feed this club. */
  categoryIds: string[]
  /** Customers with at least one stamp entry on this club. */
  memberCount: number
}

export interface LoyaltyClubRewardRequest {
  slot: number
  type: LoyaltyRewardType
  percent: number | null
  addOnId: string | null
  label: string
}

/** Rewards replace the club's in full, matched by slot; categoryIds replaces which categories feed it. */
export interface LoyaltyClubRequest {
  name: string
  style: LoyaltyClubStyle
  slotCount: number
  isActive: boolean
  sortOrder: number
  rewards: LoyaltyClubRewardRequest[]
  categoryIds: string[] | null
}

export type RewardState = 'locked' | 'available' | 'used'

/** One reward on a customer's card; earlier cards' rewards appear only while still available. */
export interface CustomerRewardDto {
  rewardId: string
  slot: number
  label: string
  type: LoyaltyRewardType
  state: RewardState
  /** Which pass round the card, 0-based. */
  cycle: number
  redemptionId: string | null
  orderId: string | null
}

/** A customer's place on one active club's card, derived from the ledger. */
export interface CustomerClubDto {
  clubId: string
  name: string
  style: LoyaltyClubStyle
  slotCount: number
  /** Slots filled on the current card. */
  position: number
  cycle: number
  rewards: CustomerRewardDto[]
  /** Drinks of this club on unpaid orders: they stamp once paid. */
  pendingDrinks: number
}

export interface LoyaltyRedemptionRequest {
  clubId: string
  rewardId: string
  cycle: number
  note: string | null
}

export type LoyaltyReason = 'Earned' | 'Redeemed' | 'Adjustment'

export interface PersonRef {
  id: string
  name: string
}

export interface CustomerSummaryDto {
  id: string
  name: string
  nickname: string | null
  notes: string | null
  isActive: boolean
  /** The tab: orders this customer pays for, minus payments. */
  outstandingBalance: number
  /** One card per active loyalty club. */
  clubs: CustomerClubDto[]
  lastOrderOn: string | null
}

export interface CustomerDetailDto extends CustomerSummaryDto {
  recentOrders: OrderSummaryDto[]
}

export interface CustomerRequest {
  name: string
  nickname: string | null
  notes: string | null
  isActive: boolean
}

export interface LoyaltyEntryDto {
  id: string
  clubId: string
  clubName: string
  delta: number
  reason: LoyaltyReason
  orderId: string | null
  notes: string | null
  createdAt: string
}

export interface LoyaltyAdjustRequest {
  clubId: string
  delta: number
  notes: string | null
}

export interface OrderSummaryDto {
  id: string
  orderedOn: string
  customer: PersonRef
  payer: PersonRef | null
  itemCount: number
  /** "Spanish Latte ×2, Americano" */
  items: string
  total: number
  paid: number
  status: PaymentStatus
}

export interface OrderItemAddOnDto {
  id: string
  addOnId: string
  name: string
  unitPrice: number
}

export interface OrderItemDto {
  id: string
  menuVersionItemId: string
  menuItemId: string
  name: string
  menuVersionName: string
  quantity: number
  unitPrice: number
  /** After the line's reward. */
  lineTotal: number
  addOns: OrderItemAddOnDto[]
  reward: OrderLineRewardDto | null
}

export interface OrderLineRewardDto {
  rewardId: string
  label: string
  type: LoyaltyRewardType
  /** ₱0 for an add-on given free that the line didn't carry. */
  amountOff: number
}

export interface PaymentDto {
  id: string
  orderId: string
  amount: number
  channel: PaymentChannel
  paidOn: string
  reference: string | null
  notes: string | null
}

export interface OrderDetailDto {
  id: string
  context: 'Office'
  orderedOn: string
  customer: PersonRef
  payer: PersonRef | null
  notes: string | null
  discountType: DiscountType
  discountValue: number
  subtotal: number
  discountAmount: number
  total: number
  paid: number
  balance: number
  status: PaymentStatus
  items: OrderItemDto[]
  payments: PaymentDto[]
  warnings: string[]
}

export interface OrderLineRequest {
  menuVersionItemId: string
  quantity: number
  addOnIds: string[]
  /** A loyalty reward on one unit; it must be available to the drinker on the drink's club. */
  rewardId?: string | null
}

export interface OrderRequest {
  customerId: string
  payerId: string | null
  orderedOn: string
  notes: string | null
  discountType: DiscountType
  discountValue: number
  items: OrderLineRequest[]
}

export interface PaymentRequest {
  amount: number
  channel: PaymentChannel
  paidOn: string
  reference: string | null
  notes: string | null
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export interface ChannelTotalDto {
  channel: PaymentChannel
  amount: number
  count: number
}

export interface TopItemDto {
  menuItemId: string
  name: string
  quantity: number
}

export interface SummaryReportDto {
  from: string
  to: string
  revenue: number
  collected: number
  outstanding: number
  orderCount: number
  drinkCount: number
  byChannel: ChannelTotalDto[]
  topItems: TopItemDto[]
  newCustomers: number
}

export interface OutstandingRowDto {
  customerId: string
  name: string
  balance: number
  oldestUnpaidOn: string
  orderCount: number
}

export interface MonthlyRowDto {
  year: number
  month: number
  revenue: number
  collected: number
  orders: number
  drinks: number
}
