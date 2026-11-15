import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { monthLabel, monthRange, shiftMonth, todayInManila } from '../../../lib/dates'
import { API, signedIn } from '../../../test/fixtures'
import { renderApp } from '../../../test/renderApp'
import { server } from '../../../test/server'

/** Serves the orders list with totals, recording each request's period. */
function serveOrders(totals: { total?: number; unpaid?: number } = { total: 1290, unpaid: 470 }) {
  const requests: { from: string | null; to: string | null }[] = []
  server.use(
    http.get(`${API}/orders`, ({ request }) => {
      const params = new URL(request.url).searchParams
      requests.push({ from: params.get('from'), to: params.get('to') })
      return HttpResponse.json({ items: [], page: 1, pageSize: 25, totalCount: 8, ...totals })
    }),
  )
  return requests
}

const thisMonth = monthRange(todayInManila())[0]
const lastMonth = shiftMonth(thisMonth, -1)

describe('Orders: the month control (handover 18)', () => {
  it('asks for this month by default and shows the whole filter’s totals', async () => {
    const requests = serveOrders()
    renderApp('/sales/orders', signedIn)

    await waitFor(() => expect(requests.at(-1)).toEqual({ from: thisMonth, to: monthRange(thisMonth)[1] }))
    expect(screen.getByRole('group', { name: 'Month' })).toHaveTextContent(monthLabel(thisMonth))
    expect(screen.queryByRole('button', { name: 'This month' })).not.toBeInTheDocument()
    const summary = await screen.findByLabelText('Orders summary')
    expect(summary).toHaveTextContent('8 orders')
    expect(summary).toHaveTextContent('Total ₱1,290')
    expect(summary).toHaveTextContent('Unpaid ₱470')
  })

  it('the previous month changes the request and the URL; This month comes back', async () => {
    const requests = serveOrders()
    const { user, router } = renderApp('/sales/orders', signedIn)

    await user.click(await screen.findByRole('button', { name: 'Previous month' }))
    await waitFor(() => expect(requests.at(-1)).toEqual({ from: lastMonth, to: monthRange(lastMonth)[1] }))
    expect(router.state.location.search).toBe(`?month=${lastMonth.slice(0, 7)}`)

    // Back to this month (RTK Query serves it from its cache, so no new request is needed).
    await user.click(screen.getByRole('button', { name: 'This month' }))
    await waitFor(() => expect(router.state.location.search).toBe(''))
    expect(screen.getByRole('group', { name: 'Month' })).toHaveTextContent(monthLabel(thisMonth))
    expect(screen.queryByRole('button', { name: 'This month' })).not.toBeInTheDocument()
  })

  it('a month in the URL is kept on load; a future or broken one falls back to this month', async () => {
    const requests = serveOrders()
    renderApp(`/sales/orders?month=${lastMonth.slice(0, 7)}`, signedIn)
    await waitFor(() => expect(requests.at(-1)?.from).toBe(lastMonth))
    expect(screen.getByRole('button', { name: 'This month' })).toBeInTheDocument()
  })

  it('ignores a future month in the URL', async () => {
    const requests = serveOrders()
    renderApp(`/sales/orders?month=${shiftMonth(thisMonth, 2).slice(0, 7)}`, signedIn)
    await waitFor(() => expect(requests.at(-1)?.from).toBe(thisMonth))
  })

  it('Custom dates sends from and to, shows Custom, and an arrow steps from the range’s month', async () => {
    const requests = serveOrders()
    const { user, router } = renderApp('/sales/orders', signedIn)

    await user.click(await screen.findByRole('button', { name: 'Custom dates' }))
    const from = screen.getByLabelText('From')
    await user.clear(from)
    await user.type(from, '2026-03-05')
    const to = screen.getByLabelText('To')
    await user.clear(to)
    await user.type(to, '2026-04-20')

    await waitFor(() => expect(requests.at(-1)).toEqual({ from: '2026-03-05', to: '2026-04-20' }))
    expect(router.state.location.search).toBe('?from=2026-03-05&to=2026-04-20')
    expect(screen.getByRole('group', { name: 'Month' })).toHaveTextContent('Custom')

    await user.click(within(screen.getByRole('group', { name: 'Month' })).getByRole('button', { name: 'Previous month' }))
    await waitFor(() => expect(requests.at(-1)).toEqual({ from: '2026-02-01', to: '2026-02-28' }))
    expect(screen.getByRole('group', { name: 'Month' })).toHaveTextContent('February 2026')
  })

  it('hides the summary line when the API sends no totals (before handover 18)', async () => {
    serveOrders({})
    renderApp('/sales/orders', signedIn)
    await screen.findByRole('group', { name: 'Month' })
    await waitFor(() => expect(screen.queryByLabelText('Orders summary')).not.toBeInTheDocument())
  })
})
