import { describe, expect, it } from 'vitest'
import { niceMax, rangeParams, showsLabel, toChartMonths } from './chartScale'

describe('chart scale', () => {
  it('rounds the maximum up to a readable number', () => {
    expect(niceMax(0)).toBe(1000)
    expect(niceMax(740)).toBe(1000)
    expect(niceMax(1240)).toBe(2000)
    expect(niceMax(2100)).toBe(2500)
    expect(niceMax(4800)).toBe(5000)
  })

  it('asks the API for 12 or 24 months ending this month, or everything', () => {
    expect(rangeParams('12', '2026-11-08')).toEqual({ from: '2025-12', to: '2026-11' })
    expect(rangeParams('24', '2026-11-08')).toEqual({ from: '2024-12', to: '2026-11' })
    expect(rangeParams('all', '2026-11-08')).toEqual({ to: '2026-11' })
  })

  it('labels months with the year on January and on the first bar', () => {
    const months = toChartMonths([
      { year: 2025, month: 12, revenue: 1, collected: 1 },
      { year: 2026, month: 1, revenue: 2, collected: 2 },
      { year: 2026, month: 2, revenue: 3, collected: 3 },
    ])
    expect(months.map((m) => m.label)).toEqual(['Dec 25', 'Jan 26', 'Feb'])
    expect(months[2]).toMatchObject({ key: '2026-02', revenue: 3 })
  })

  it('labels every month up to 36 bars, then every third', () => {
    expect(Array.from({ length: 36 }, (_, i) => showsLabel(i, 36)).every(Boolean)).toBe(true)
    const shown = Array.from({ length: 40 }, (_, i) => i).filter((i) => showsLabel(i, 40))
    expect(shown).toEqual([0, 3, 6, 9, 12, 15, 18, 21, 24, 27, 30, 33, 36, 39])
  })
})
