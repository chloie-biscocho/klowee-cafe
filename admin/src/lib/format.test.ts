import { describe, expect, it } from 'vitest'
import { formatDate, formatDateRange, formatPeso } from './format'

describe('formatPeso', () => {
  it('drops the decimals when the price is whole', () => {
    expect(formatPeso(150)).toBe('₱150')
    expect(formatPeso(1500)).toBe('₱1,500')
  })

  it('always shows two decimals when the price is not whole', () => {
    expect(formatPeso(99.5)).toBe('₱99.50')
    expect(formatPeso(99.55)).toBe('₱99.55')
  })
})

describe('formatDate', () => {
  it('reads the date as written, with no time-zone shift', () => {
    expect(formatDate('2026-04-16')).toBe('Apr 16, 2026')
    expect(formatDate('2026-01-01')).toBe('Jan 1, 2026')
  })
})

describe('formatDateRange', () => {
  it('writes a range within one month the way a poster does', () => {
    expect(formatDateRange('2026-05-21', '2026-05-24')).toBe('May 21 to 24, 2026')
  })

  it('shows a one-day event once', () => {
    expect(formatDateRange('2026-05-21', '2026-05-21')).toBe('May 21, 2026')
  })

  it('names both months across a month, and both years across a year', () => {
    expect(formatDateRange('2026-05-30', '2026-06-02')).toBe('May 30 to Jun 2, 2026')
    expect(formatDateRange('2026-12-30', '2027-01-02')).toBe('Dec 30, 2026 to Jan 2, 2027')
  })
})
