import { describe, expect, it } from 'vitest'
import { formatPaidOn, stampTilt } from './format'

// 14 Nov 2026, 10:00 in Manila.
const NOW = new Date('2026-11-14T02:00:00Z')

describe('formatPaidOn (handover 17a)', () => {
  it('leaves the year off for this year, up to today', () => {
    expect(formatPaidOn('2026-11-10', NOW)).toBe('Nov 10')
    expect(formatPaidOn('2026-11-14', NOW)).toBe('Nov 14')
  })

  it('adds the year for another year, so last December does not read as a coming one', () => {
    expect(formatPaidOn('2025-12-06', NOW)).toBe('Dec 6, 2025')
  })

  it('adds the year for a date after today: a wrong date in the records shows as one', () => {
    expect(formatPaidOn('2026-12-06', NOW)).toBe('Dec 6, 2026')
  })
})

describe('stampTilt', () => {
  it('matches the admin formula: slots 1 to 12 all differ, within 12 degrees', () => {
    const tilts = Array.from({ length: 12 }, (_, i) => stampTilt(i + 1))
    expect(new Set(tilts).size).toBe(12)
    expect(Math.max(...tilts.map(Math.abs))).toBeLessThanOrEqual(12)
  })
})
