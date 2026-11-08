import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { MonthlyChart, type ChartMonth } from './MonthlyChart'

const months = (count: number): ChartMonth[] =>
  Array.from({ length: count }, (_, i) => ({ key: `m${i}`, label: `M${i}`, revenue: 100, collected: 50 }))

describe('MonthlyChart labels', () => {
  it('labels every month up to 36 bars', () => {
    render(<MonthlyChart months={months(36)} label="test" />)
    const chart = screen.getByRole('img')
    expect(within(chart).getAllByText(/^M\d+$/)).toHaveLength(36)
  })

  it('collapses to every third label beyond 36 bars, keeping every bar', () => {
    render(<MonthlyChart months={months(40)} label="test" />)
    const chart = screen.getByRole('img')
    expect(chart.querySelectorAll('[data-testid^="chart-month-"]')).toHaveLength(40)
    expect(within(chart).getAllByText(/^M\d+$/).map((t) => t.textContent)).toEqual(
      ['M0', 'M3', 'M6', 'M9', 'M12', 'M15', 'M18', 'M21', 'M24', 'M27', 'M30', 'M33', 'M36', 'M39'],
    )
  })
})
