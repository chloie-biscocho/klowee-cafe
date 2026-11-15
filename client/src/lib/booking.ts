import type { PublicHome } from '../api/types'
import { setting } from './content'

/**
 * The booking form's rules, mirroring the API's (handover 20). The API checks
 * them again: these only save a round trip and say what's wrong under the field.
 */
export const LIMITS = { name: 100, contact: 200, venue: 200, message: 1000, minGuests: 1, maxGuests: 5000, yearsAhead: 2 } as const

/** The business's time zone (decision 006): "today" is Manila's date, wherever the visitor is. */
export const BUSINESS_TIME_ZONE = 'Asia/Manila'

/** The form is on unless the owners set it to exactly "false"; nothing saved means on (the API's rule). */
export function bookingFormEnabled(home: PublicHome): boolean {
  return setting(home, 'booking_form_enabled').toLowerCase() !== 'false'
}

/** The Book section's numbered steps (handover 20a): one per non-blank line, at most three (the API's rule). */
export function bookingSteps(home: PublicHome): string[] {
  return setting(home, 'booking_steps')
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .slice(0, 3)
}

/** Today in Manila as YYYY-MM-DD (the date input's format). */
export function businessToday(now: Date = new Date()): string {
  // en-CA formats dates as YYYY-MM-DD.
  return new Intl.DateTimeFormat('en-CA', { timeZone: BUSINESS_TIME_ZONE, year: 'numeric', month: '2-digit', day: '2-digit' }).format(now)
}

/** The latest date the API accepts: the same day two years on (28 February for a 29th). */
export function latestEventDate(today: string): string {
  const [year, month, day] = today.split('-').map(Number) as [number, number, number]
  const target = year + LIMITS.yearsAhead
  const lastDay = new Date(Date.UTC(target, month, 0)).getUTCDate()
  return `${target}-${String(month).padStart(2, '0')}-${String(Math.min(day, lastDay)).padStart(2, '0')}`
}

export interface BookingValues {
  name: string
  contact: string
  eventDate: string
  venue: string
  guestCount: string
  /** '' is "Not sure yet". */
  packageId: string
  message: string
  /** The honeypot; people never see it. */
  website: string
}

/** A package card's "Book this": which package, and a counter so the same card twice still counts as a new choice. */
export interface PackageChoice {
  packageId: string | null
  nonce: number
}

/** No package card pressed yet. */
export const NO_PACKAGE_CHOICE: PackageChoice = { packageId: null, nonce: 0 }

export type BookingField = Exclude<keyof BookingValues, 'website'>

/** On-screen order: the first problem in this order gets focus. */
export const FIELD_ORDER: BookingField[] = ['name', 'contact', 'eventDate', 'venue', 'guestCount', 'packageId', 'message']

export const EMPTY_BOOKING: BookingValues = { name: '', contact: '', eventDate: '', venue: '', guestCount: '', packageId: '', message: '', website: '' }

export type BookingErrors = Partial<Record<BookingField, string>>

const tooLong = (max: number) => `Please keep this to ${max} characters.`

/** What's wrong, per field, in the API's words. Empty when the form can be sent. */
export function validateBooking(values: BookingValues, today: string): BookingErrors {
  const errors: BookingErrors = {}
  const name = values.name.trim()
  const contact = values.contact.trim()
  const venue = values.venue.trim()

  if (!name) errors.name = 'Please tell us your name.'
  else if (name.length > LIMITS.name) errors.name = tooLong(LIMITS.name)
  if (!contact) errors.contact = 'Please tell us how to reach you.'
  else if (contact.length > LIMITS.contact) errors.contact = tooLong(LIMITS.contact)

  // The input's value is always YYYY-MM-DD or empty, so strings compare as dates.
  if (!values.eventDate) errors.eventDate = 'Please pick the event date.'
  else if (values.eventDate < today) errors.eventDate = 'That date has passed. Please pick today or later.'
  else if (values.eventDate > latestEventDate(today)) errors.eventDate = `Please pick a date within ${LIMITS.yearsAhead} years.`

  if (!venue) errors.venue = 'Please tell us where the event is.'
  else if (venue.length > LIMITS.venue) errors.venue = tooLong(LIMITS.venue)

  const guests = values.guestCount.trim()
  if (guests !== '') {
    const count = Number(guests)
    if (!Number.isInteger(count) || count < LIMITS.minGuests || count > LIMITS.maxGuests)
      errors.guestCount = `Guests can be ${LIMITS.minGuests} to ${LIMITS.maxGuests.toLocaleString('en-US')}.`
  }

  if (values.message.trim().length > LIMITS.message) errors.message = tooLong(LIMITS.message)
  return errors
}

const apiUrl = import.meta.env.VITE_API_URL
export const BOOKINGS_URL = `${(apiUrl ?? '').replace(/\/$/, '')}/api/public/bookings`

export type BookingResult =
  | { ok: true }
  /** Field errors from the API, plus anything it said that isn't about one field. */
  | { ok: false; fields: BookingErrors; message: string | null }

const NETWORK_MESSAGE = "Couldn't reach Klowee. Check your connection and try again; what you typed is still here."
const BUSY_MESSAGE = "You've sent a few requests already. Please try again later, or message Klowee below."

/** The API's error keys ("eventDate", "$.guestCount", "GuestCount") as our field names, or null. */
function fieldOf(key: string): BookingField | null {
  const bare = key.replace(/^\$\./, '').replace(/^request\./i, '')
  const camel = bare.charAt(0).toLowerCase() + bare.slice(1)
  return (FIELD_ORDER as string[]).includes(camel) ? (camel as BookingField) : null
}

/**
 * One POST with plain fetch (decision 011 as amended by handover 17): the
 * answer is only "received", so there's nothing to cache or refetch.
 */
export async function sendBooking(values: BookingValues): Promise<BookingResult> {
  const guests = values.guestCount.trim()
  const body = {
    name: values.name.trim(),
    contact: values.contact.trim(),
    eventDate: values.eventDate,
    venue: values.venue.trim(),
    guestCount: guests === '' ? null : Number(guests),
    packageId: values.packageId || null,
    message: values.message.trim() || null,
    website: values.website,
  }

  let response: Response
  try {
    response = await fetch(BOOKINGS_URL, {
      method: 'POST',
      headers: { accept: 'application/json', 'content-type': 'application/json' },
      body: JSON.stringify(body),
    })
  } catch {
    return { ok: false, fields: {}, message: NETWORK_MESSAGE }
  }
  if (response.ok) return { ok: true }

  let problem: { detail?: string; errors?: Record<string, string[]> } = {}
  try {
    problem = (await response.json()) as typeof problem
  } catch {
    // No body: fall through to the general message.
  }
  if (response.status === 429) return { ok: false, fields: {}, message: problem.detail ?? BUSY_MESSAGE }

  const fields: BookingErrors = {}
  const unplaced: string[] = []
  for (const [key, messages] of Object.entries(problem.errors ?? {})) {
    const field = fieldOf(key)
    const text = messages[0] ?? 'Please check this.'
    if (field && !fields[field]) fields[field] = text
    else if (!field) unplaced.push(text)
  }
  const placed = Object.keys(fields).length > 0
  const message =
    unplaced[0] ?? (placed ? null : (problem.detail ?? 'Something went wrong. Please try again, or message Klowee below.'))
  return { ok: false, fields, message }
}
