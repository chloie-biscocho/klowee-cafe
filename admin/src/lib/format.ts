/** Display formatting. Nothing here talks to the API or to React. */

const peso = new Intl.NumberFormat('en-PH', {
  style: 'currency',
  currency: 'PHP',
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
})

/**
 * Prices the way the owners write them: `₱150` when the amount is whole,
 * `₱99.50` when it is not. Intl keeps the thousands separator (`₱1,500`).
 */
export function formatPeso(amount: number): string {
  return Number.isInteger(amount)
    ? peso.format(amount)
    : peso.format(amount).replace(/(\.\d)$/, '$10')
}

const dateFormat = new Intl.DateTimeFormat('en-PH', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
})

/**
 * Formats the API's `DateOnly` strings ("2026-04-16") as "Apr 16, 2026".
 * Parsed as UTC on purpose: a plain date has no time zone, and letting the
 * browser localise it would shift the day for anyone west of Manila.
 */
export function formatDate(isoDate: string): string {
  const [year, month, day] = isoDate.split('-').map(Number)
  if (!year || !month || !day) return isoDate
  return dateFormat.format(new Date(Date.UTC(year, month - 1, day)))
}

/** Today as the API writes dates, for form defaults. */
export function todayIsoDate(): string {
  const now = new Date()
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 10)
}
