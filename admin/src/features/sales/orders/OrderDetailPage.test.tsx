import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../../test/fixtures'
import { renderApp } from '../../../test/renderApp'
import { unpaidOrder } from '../../../test/salesFixtures'
import { problem, server } from '../../../test/server'
import type { OrderDetailDto, PaymentRequest } from '../../../types/api'

describe('Order detail', () => {
  it('adds a payment and shows the new status without a reload', async () => {
    let current: OrderDetailDto = unpaidOrder
    let sent: PaymentRequest | null = null
    server.use(
      http.get(`${API}/orders/:id`, () => HttpResponse.json(current)),
      http.post(`${API}/orders/:id/payments`, async ({ request }) => {
        sent = (await request.json()) as PaymentRequest
        current = {
          ...unpaidOrder,
          paid: 100,
          balance: 90,
          status: 'Partial',
          payments: [{ id: 'p1', orderId: unpaidOrder.id, amount: 100, channel: 'Cash', paidOn: sent.paidOn, reference: null, notes: null }],
        }
        return HttpResponse.json(current)
      }),
    )
    const { user } = renderApp(`/sales/orders/${unpaidOrder.id}`, signedIn)

    expect(await screen.findByText('Unpaid')).toBeInTheDocument()
    const form = screen.getByRole('form', { name: 'Add payment' })
    // The amount starts at what's owed.
    expect(within(form).getByLabelText('Amount')).toHaveValue(190)
    await user.clear(within(form).getByLabelText('Amount'))
    await user.type(within(form).getByLabelText('Amount'), '100')
    await user.selectOptions(within(form).getByLabelText('Channel'), 'Cash')
    await user.click(within(form).getByRole('button', { name: 'Add payment' }))

    expect(await screen.findByText('Partial')).toBeInTheDocument()
    expect(sent).toMatchObject({ amount: 100, channel: 'Cash', reference: null })
    expect(screen.getByText('Payment of ₱100 recorded.')).toBeInTheDocument()
    await waitFor(() => expect(within(screen.getByRole('form', { name: 'Add payment' })).getByLabelText('Amount')).toHaveValue(90))
  })

  it('shows an overpayment refusal as a toast', async () => {
    server.use(
      http.post(`${API}/orders/:id/payments`, () =>
        problem(409, 'Conflict', '₱500.00 is more than this order owes (₱190.00).'),
      ),
    )
    const { user } = renderApp(`/sales/orders/${unpaidOrder.id}`, signedIn)
    const form = await screen.findByRole('form', { name: 'Add payment' })
    await user.clear(within(form).getByLabelText('Amount'))
    await user.type(within(form).getByLabelText('Amount'), '500')
    await user.click(within(form).getByRole('button', { name: 'Add payment' }))

    expect(await screen.findByText('₱500.00 is more than this order owes (₱190.00).')).toBeInTheDocument()
    expect(screen.getByText('Unpaid')).toBeInTheDocument()
  })

  it('locks editing once the order has payments', async () => {
    server.use(
      http.get(`${API}/orders/:id`, () =>
        HttpResponse.json({ ...unpaidOrder, payments: [{ id: 'p1', orderId: unpaidOrder.id, amount: 50, channel: 'Cash', paidOn: '2026-11-08', reference: null, notes: null }] }),
      ),
    )
    renderApp(`/sales/orders/${unpaidOrder.id}`, signedIn)
    expect(await screen.findByRole('button', { name: 'Edit' })).toBeDisabled()
  })
})
