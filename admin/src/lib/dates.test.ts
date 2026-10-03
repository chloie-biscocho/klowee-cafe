import { describe, expect, it } from 'vitest'
import {
  formatManilaDateTime,
  isoToManilaLocal,
  manilaLocalToIso,
  todayInManila,
} from './dates'

describe('manilaLocalToIso / isoToManilaLocal', () => {
  it('reads an evening datetime-local value as Manila time', () => {
    const iso = manilaLocalToIso('2026-05-21T20:00')
    expect(iso).toBe('2026-05-21T20:00:00+08:00')
    expect(new Date(iso).toISOString()).toBe('2026-05-21T12:00:00.000Z')
    // And back, from the UTC spelling the API returns.
    expect(isoToManilaLocal('2026-05-21T12:00:00+00:00')).toBe('2026-05-21T20:00')
  })

  it('crosses the day boundary: early morning in Manila is the previous day in UTC', () => {
    const iso = manilaLocalToIso('2026-05-22T03:30')
    expect(new Date(iso).toISOString()).toBe('2026-05-21T19:30:00.000Z')
    expect(isoToManilaLocal('2026-05-21T19:30:00Z')).toBe('2026-05-22T03:30')
  })

  it('crosses the year boundary the other way: late UTC on Dec 31 is New Year in Manila', () => {
    expect(isoToManilaLocal('2026-12-31T16:00:00Z')).toBe('2027-01-01T00:00')
    expect(new Date(manilaLocalToIso('2027-01-01T00:00')).toISOString()).toBe(
      '2026-12-31T16:00:00.000Z',
    )
  })

  it('rejects anything that is not a datetime-local value', () => {
    expect(() => manilaLocalToIso('')).toThrow()
    expect(() => manilaLocalToIso('2026-05-21')).toThrow()
  })
})

describe('display helpers', () => {
  it('formats an instant in Manila time whatever zone the machine is in', () => {
    expect(formatManilaDateTime('2026-05-21T12:00:00Z')).toBe('May 21, 2026, 8:00 PM')
  })

  it("knows today's date in Manila", () => {
    expect(todayInManila(new Date('2026-05-21T17:00:00Z'))).toBe('2026-05-22')
    expect(todayInManila(new Date('2026-05-21T15:59:00Z'))).toBe('2026-05-21')
  })
})
