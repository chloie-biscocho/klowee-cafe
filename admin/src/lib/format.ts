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

const monthDay = new Intl.DateTimeFormat('en-PH', { day: 'numeric', month: 'short', timeZone: 'UTC' })

/**
 * An event's dates the way a poster writes them: "May 21 to 24, 2026", or
 * "May 30 to Jun 2, 2026" across a month, with the year repeated only when it
 * changes. A one-day event shows its date once.
 */
export function formatDateRange(startsOn: string, endsOn: string): string {
  if (startsOn === endsOn) return formatDate(startsOn)

  const [startYear, startMonth] = startsOn.split('-')
  const [endYear, endMonth, endDay] = endsOn.split('-')
  if (!startYear || !endYear || !endDay) return `${startsOn} to ${endsOn}`

  if (startYear !== endYear) return `${formatDate(startsOn)} to ${formatDate(endsOn)}`

  const start = monthDay.format(new Date(`${startsOn}T00:00:00Z`))
  const end =
    startMonth === endMonth
      ? String(Number(endDay))
      : monthDay.format(new Date(`${endsOn}T00:00:00Z`))
  return `${start} to ${end}, ${startYear}`
}

/** Today as the API writes dates, for form defaults. */
export function todayIsoDate(): string {
  const now = new Date()
  const local = new Date(now.getTime() - now.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 10)
}
