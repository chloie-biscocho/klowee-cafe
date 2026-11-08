import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../../test/fixtures'
import { renderApp } from '../../../test/renderApp'
import { kee, prince } from '../../../test/salesFixtures'
import { server } from '../../../test/server'
import type { PaymentDto, PaymentRequest } from '../../../types/api'

describe('Settle tab', () => {
  it('records the amount and lists the payments the API spread it over', async () => {
    let sent: PaymentRequest | null = null
    const payments: PaymentDto[] = [
      { id: 'p1', orderId: 'o1aaaaaa-0000', amount: 190, channel: 'BPI', paidOn: '2026-11-08', reference: 'BPI-7781', notes: null },
      { id: 'p2', orderId: 'o2bbbbbb-0000', amount: 130, channel: 'BPI', paidOn: '2026-11-08', reference: 'BPI-7781', notes: null },
    ]
    server.use(
      http.post(`${API}/customers/:id/settle`, async ({ request }) => {
        sent = (await request.json()) as PaymentRequest
        return HttpResponse.json(payments)
      }),
    )
    const { user } = renderApp(`/sales/customers/${kee.id}`, signedIn)

    await user.click(await screen.findByRole('button', { name: 'Settle tab' }))
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByLabelText('Amount')).toHaveValue(320)
    await user.selectOptions(within(dialog).getByLabelText('Channel'), 'BPI')
    await user.type(within(dialog).getByLabelText('Reference'), 'BPI-7781')
    await user.click(within(dialog).getByRole('button', { name: 'Record payment' }))

    expect(await within(dialog).findByText(/Spread over 2 orders/)).toBeInTheDocument()
    expect(within(dialog).getByText('Order o1aaaaaa')).toBeInTheDocument()
    expect(within(dialog).getByText(/₱130/)).toBeInTheDocument()
    expect(sent).toMatchObject({ amount: 320, channel: 'BPI', reference: 'BPI-7781' })
  })
})

describe('Merge into', () => {
  it('needs a confirming second step that restates both names, then posts to the right endpoint', async () => {
    let called: string | null = null
    server.use(
      http.post(`${API}/customers/:id/merge-into/:targetId`, ({ params }) => {
        called = `${params.id}->${params.targetId}`
        return HttpResponse.json({ ...prince, recentOrders: [] })
      }),
    )
    const { user, router } = renderApp(`/sales/customers/${kee.id}`, signedIn)

    await user.click(await screen.findByRole('button', { name: 'Merge into…' }))
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByRole('button', { name: 'Continue' })).toBeDisabled()

    const keep = within(dialog).getByRole('combobox', { name: 'Keep' })
    await user.type(keep, 'Prin{Enter}')
    await user.click(within(dialog).getByRole('button', { name: 'Continue' }))

    // Nothing is sent until the restated confirmation is accepted.
    expect(called).toBeNull()
    expect(within(dialog).getByText(/cannot be undone/)).toBeInTheDocument()
    expect(within(dialog).getAllByText('Kee').length).toBeGreaterThan(0)
    expect(within(dialog).getByText('Prince')).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Merge for good' }))
    await screen.findByText('Kee merged into Prince.')
    expect(called).toBe(`${kee.id}->${prince.id}`)
    expect(router.state.location.pathname).toBe(`/sales/customers/${prince.id}`)
  })
})
