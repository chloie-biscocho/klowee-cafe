/** The smallest "round" number at or above the max (1, 2, 2.5 or 5 × a power of ten), so gridlines land on readable values. */
export function niceMax(value: number): number {
  if (value <= 0) return 1000
  const power = 10 ** Math.floor(Math.log10(value))
  return ([1, 2, 2.5, 5, 10].find((step) => step * power >= value) ?? 10) * power
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

/** The twelve months ending with the one `isoDate` falls in, oldest first: [{ year, month (1–12), key, label }]. */
export function lastTwelveMonths(isoDate: string) {
  const [year, month] = isoDate.split('-').map(Number) as [number, number]
  return Array.from({ length: 12 }, (_, index) => {
    const offset = month - 11 + index // 1-based month; ≤ 0 means last year
    const y = offset <= 0 ? year - 1 : year
    const m = offset <= 0 ? offset + 12 : offset
    return { year: y, month: m, key: `${y}-${String(m).padStart(2, '0')}`, label: `${MONTHS[m - 1]}${m === 1 || index === 0 ? ` ${String(y).slice(2)}` : ''}` }
  })
}
