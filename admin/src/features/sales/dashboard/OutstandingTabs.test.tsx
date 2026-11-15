import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import type { OutstandingRowDto } from '../../../types/api'
import { OutstandingTabs, VISIBLE_ROWS } from './OutstandingTabs'

const tabs = (count: number): OutstandingRowDto[] =>
  Array.from({ length: count }, (_, i) => ({
    customerId: `c0000000-0000-0000-0000-${String(i + 1).padStart(12, '0')}`,
    name: `Customer ${i + 1}`,
    balance: 1000 - i * 10,
    oldestUnpaidOn: '2026-10-01',
    orderCount: 1,
  }))

function show(rows: OutstandingRowDto[], onSettle = vi.fn()) {
  const router = createMemoryRouter([{ path: '/', element: <OutstandingTabs rows={rows} onSettle={onSettle} /> }])
  render(<RouterProvider router={router} />)
  return onSettle
}

describe('Outstanding tabs', () => {
  it('with 8 tabs, shows every row with no scroll region, and the count in the title', () => {
    expect(VISIBLE_ROWS).toBe(8)
    show(tabs(VISIBLE_ROWS))

    expect(screen.getByRole('heading', { name: 'Outstanding tabs (8)' })).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Outstanding tabs, scrollable' })).not.toBeInTheDocument()
    expect(screen.queryByTestId('outstanding-scroll')).not.toBeInTheDocument()
    expect(screen.getAllByRole('row')).toHaveLength(1 + 8)
  })

  it('with 9 or more, puts every row inside a labelled, focusable scroll region, in the same order', async () => {
    const rows = tabs(25)
    const onSettle = show(rows)

    expect(screen.getByRole('heading', { name: 'Outstanding tabs (25)' })).toBeInTheDocument()
    const region = screen.getByRole('region', { name: 'Outstanding tabs, scrollable' })
    expect(region).toHaveAttribute('tabindex', '0')
    const bodyRows = within(region).getAllByRole('row').slice(1)
    expect(bodyRows.map((row) => within(row).getByRole('link').textContent)).toEqual(rows.map((r) => r.name))
    // The header stays visible with a solid background.
    for (const header of within(region).getAllByRole('columnheader')) expect(header).toHaveClass('sticky', 'top-0', 'bg-surface')

    // Keyboard users can reach it; Settle and the links work as before.
    const user = userEvent.setup()
    await user.tab()
    expect(region).toHaveFocus()
    await user.click(within(bodyRows[20]!).getByRole('button', { name: 'Settle' }))
    expect(onSettle).toHaveBeenCalledWith(rows[20])
    expect(within(bodyRows[20]!).getByRole('link', { name: 'Customer 21' })).toHaveAttribute('href', `/sales/customers/${rows[20]!.customerId}`)
  })

  it('says when nobody owes anything', () => {
    show([])
    expect(screen.getByRole('heading', { name: 'Outstanding tabs (0)' })).toBeInTheDocument()
    expect(screen.getByText("No open tabs. Everyone's paid up.")).toBeInTheDocument()
  })
})
