import { describe, expect, it } from 'vitest'
import { lastTwelveMonths, niceMax } from './chartScale'

describe('chart scale', () => {
  it('rounds the maximum up to a readable number', () => {
    expect(niceMax(0)).toBe(1000)
    expect(niceMax(740)).toBe(1000)
    expect(niceMax(1240)).toBe(2000)
    expect(niceMax(2100)).toBe(2500)
    expect(niceMax(4800)).toBe(5000)
  })

  it('lists the twelve months ending this month, across the year boundary', () => {
    const months = lastTwelveMonths('2026-11-08')
    expect(months).toHaveLength(12)
    expect(months[0]).toMatchObject({ year: 2025, month: 12, label: 'Dec 25' })
    expect(months[1]).toMatchObject({ year: 2026, month: 1, label: 'Jan 26' })
    expect(months[11]).toMatchObject({ year: 2026, month: 11, key: '2026-11', label: 'Nov' })
  })
})
