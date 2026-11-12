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
}

export interface MenuCategoryRequest {
  name: string
  sortOrder: number
  description: string | null
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
}

export interface MenuItemRequest {
  name: string
  description: string | null
  categoryId: string
  photoUrl: string | null
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

export const DISCOUNT_TYPES = ['None', 'Percent', 'Fixed', 'LoyaltyFree'] as const
export type DiscountType = (typeof DISCOUNT_TYPES)[number]

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
  loyaltyBalance: number
  availableRewards: number
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
  delta: number
  reason: LoyaltyReason
  orderId: string | null
  notes: string | null
  createdAt: string
}

export interface LoyaltyAdjustRequest {
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
  lineTotal: number
  addOns: OrderItemAddOnDto[]
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
