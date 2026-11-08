import { screen, waitFor, within } from '@testing-library/react'
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

    const chart = screen.getByRole('img', { name: /last twelve months/i })
    expect(chart.querySelectorAll('[data-testid^="chart-month-"]')).toHaveLength(12)
    // Each month is two bars plus a hover band.
    const thisMonth = todayInManila().slice(0, 7)
    expect(within(chart).getByTestId(`chart-month-${thisMonth}`).querySelectorAll('rect')).toHaveLength(3)
  })
})
