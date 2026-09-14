import { describe, expect, it } from 'vitest'
import { formatDate, formatPeso } from './format'

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
