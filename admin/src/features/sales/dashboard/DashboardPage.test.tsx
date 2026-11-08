import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { signedIn } from '../../../test/fixtures'
import { todayInManila } from '../../../lib/dates'
import { renderApp } from '../../../test/renderApp'

describe('Dashboard', () => {
  it('is where the app lands, with the month stat cards, open tabs, top drinks and the chart', async () => {
    const { router } = renderApp('/', signedIn)

    await waitFor(() => expect(screen.getByLabelText('Revenue')).toHaveTextContent('₱1,250'))
    expect(screen.getByLabelText('Collected')).toHaveTextContent('₱940')
    expect(screen.getByLabelText('Outstanding')).toHaveTextContent('₱610')
    expect(screen.getByLabelText('Drinks sold')).toHaveTextContent('9')
    expect(router.state.location.pathname).toBe('/')

    const tabs = screen.getByRole('heading', { name: 'Outstanding tabs' }).closest('div')!.parentElement!
    expect(within(tabs).getByText('Kee')).toBeInTheDocument()
    expect(within(tabs).getByRole('button', { name: 'Settle' })).toBeInTheDocument()

    expect(screen.getByText('Spanish Latte')).toBeInTheDocument()

    const chart = await screen.findByRole('img', { name: /last 12 months/i })
    await waitFor(() => expect(chart.querySelectorAll('[data-testid^="chart-month-"]')).toHaveLength(12))
    expect(chart.querySelectorAll('[data-testid^="chart-month-"]')).toHaveLength(12)
    // Each month is two bars plus a hover band.
    const thisMonth = todayInManila().slice(0, 7)
    expect(within(chart).getByTestId(`chart-month-${thisMonth}`).querySelectorAll('rect')).toHaveLength(3)
  })

  it('shows every month since the first order on All', async () => {
    renderApp('/', signedIn)
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: 'All' }))

    const chart = screen.getByRole('img', { name: /every month since the first order/i })
    const [year, month] = todayInManila().split('-').map(Number) as [number, number]
    const sinceMay2025 = (year - 2025) * 12 + month - 5 + 1
    await waitFor(() => expect(chart.querySelectorAll('[data-testid^="chart-month-"]')).toHaveLength(sinceMay2025))
    expect(within(chart).getByTestId('chart-month-2025-05')).toHaveTextContent('May 25')
    expect(screen.getByRole('button', { name: 'All' })).toHaveAttribute('aria-pressed', 'true')
  })
})
