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
}

export interface MenuCategoryRequest {
  name: string
  sortOrder: number
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
