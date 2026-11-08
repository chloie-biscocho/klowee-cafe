/** The smallest "round" number at or above the max (1, 2, 2.5 or 5 × a power of ten), so gridlines land on readable values. */
export function niceMax(value: number): number {
  if (value <= 0) return 1000
  const power = 10 ** Math.floor(Math.log10(value))
  return ([1, 2, 2.5, 5, 10].find((step) => step * power >= value) ?? 10) * power
}

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

export type ChartRange = '12' | '24' | 'all'

/** The `/reports/monthly` range for a choice, ending with the month `isoDate` falls in. "all" leaves `from` out: the API starts at the first month with an order. */
export function rangeParams(range: ChartRange, isoDate: string): { from?: string; to: string } {
  const to = isoDate.slice(0, 7)
  if (range === 'all') return { to }
  const [year, month] = isoDate.split('-').map(Number) as [number, number]
  const start = new Date(Date.UTC(year, month - Number(range), 1))
  return { from: start.toISOString().slice(0, 7), to }
}

/** API rows → chart months, labelled "Nov", with the year on January and on the first bar ("Dec 25"). */
export function toChartMonths(rows: { year: number; month: number; revenue: number; collected: number }[]) {
  return rows.map((row, index) => ({
    key: `${row.year}-${String(row.month).padStart(2, '0')}`,
    label: `${MONTHS[row.month - 1]}${row.month === 1 || index === 0 ? ` ${String(row.year).slice(2)}` : ''}`,
    revenue: row.revenue,
    collected: row.collected,
  }))
}

/** Up to 36 bars every month is labelled; beyond that only every third, starting with the first, so labels never overlap. */
export function showsLabel(index: number, count: number): boolean {
  return count <= 36 || index % 3 === 0
}
