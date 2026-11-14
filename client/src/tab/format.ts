/** Dates on the tab page. Plain dates are read as UTC, so no visitor's zone can shift the day. */

const asUtc = (isoDate: string) => new Date(`${isoDate}T00:00:00Z`)
const shortDate = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', timeZone: 'UTC' })
const longDate = new Intl.DateTimeFormat('en-US', { month: 'long', day: 'numeric', timeZone: 'UTC' })

/** "Nov 13" */
export const formatShortDate = (isoDate: string) => shortDate.format(asUtc(isoDate))

/** "November 13", for screen readers. */
export const formatLongDate = (isoDate: string) => longDate.format(asUtc(isoDate))

/** "2026-11-13" → "11/13", the way the owners write it above a stamp (as the admin does). */
export function stampDate(isoDate: string): string {
  const [, month, day] = isoDate.split('-').map(Number)
  return `${month}/${day}`
}

/**
 * The same fixed tilt as the admin's stamps (`stampTilt` in admin/src/lib/loyalty.ts,
 * handover 16b), so both apps show the same card: −12 to 12 degrees, a pure
 * function of the slot number.
 */
export const stampTilt = (slot: number) => ((slot * 7 + 3) % 25) - 12

/** "The Coffee Club" → "Coffee Club". */
export const clubLabel = (name: string) => name.replace(/^the\s+/i, '')

/** 1 → "1st", 12 → "12th". */
export function ordinal(n: number): string {
  const tens = n % 100
  if (tens >= 11 && tens <= 13) return `${n}th`
  return `${n}${({ 1: 'st', 2: 'nd', 3: 'rd' } as Record<number, string>)[n % 10] ?? 'th'}`
}

/** "/tab/AbC…/" → "AbC…"; anything unreadable is an empty token (a dead link). */
export function tokenFromPath(path: string): string {
  const segment = path.replace(/^\/tab\//, '').split('/')[0] ?? ''
  try {
    return decodeURIComponent(segment)
  } catch {
    return ''
  }
}
