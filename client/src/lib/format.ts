/** Display formatting, the same rules as the admin app's `lib/format.ts`. */

const peso = new Intl.NumberFormat('en-PH', {
  style: 'currency',
  currency: 'PHP',
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
})

/** `₱150` when whole, `₱99.50` when not, `₱7,500` with the separator. */
export function formatPeso(amount: number): string {
  return Number.isInteger(amount)
    ? peso.format(amount)
    : peso.format(amount).replace(/(\.\d)$/, '$10')
}

// Plain dates are formatted as UTC so no visitor's zone can shift the day.
const fullDate = new Intl.DateTimeFormat('en-PH', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  timeZone: 'UTC',
})
const monthDay = new Intl.DateTimeFormat('en-PH', { day: 'numeric', month: 'short', timeZone: 'UTC' })

const asUtc = (isoDate: string) => new Date(`${isoDate}T00:00:00Z`)

export function formatDate(isoDate: string): string {
  return fullDate.format(asUtc(isoDate))
}

/** "May 21 to 24, 2026"; one day shows once; months and years only when they change. */
export function formatDateRange(startsOn: string, endsOn: string): string {
  if (startsOn === endsOn) return formatDate(startsOn)
  if (startsOn.slice(0, 4) !== endsOn.slice(0, 4)) {
    return `${formatDate(startsOn)} to ${formatDate(endsOn)}`
  }
  const end =
    startsOn.slice(0, 7) === endsOn.slice(0, 7)
      ? String(Number(endsOn.slice(8, 10)))
      : monthDay.format(asUtc(endsOn))
  return `${monthDay.format(asUtc(startsOn))} to ${end}, ${startsOn.slice(0, 4)}`
}

/** Today's date in Manila ("2026-05-21"), the business date the API uses. Fixed +08:00, no DST. */
export function todayInManila(now: Date = new Date()): string {
  return new Date(now.getTime() + 8 * 60 * 60 * 1000).toISOString().slice(0, 10)
}
