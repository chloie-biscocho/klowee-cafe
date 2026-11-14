import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../../test/fixtures'
import { renderApp } from '../../../test/renderApp'
import { kee, prince, summary } from '../../../test/salesFixtures'
import { problem, server } from '../../../test/server'
import type { CardRequestDto, PaymentClaimDto } from '../../../types/api'

const claim: PaymentClaimDto = {
  id: 'pc000000-0000-0000-0000-000000000001',
  customerId: kee.id,
  customerName: 'Kee',
  amount: 320,
  channel: 'BusinessGCash',
  paymentMethodLabel: 'GCash',
  reference: 'GC12345678',
  status: 'Pending',
  createdAt: '2026-11-08T01:15:00Z',
  resolvedAt: null,
  rejectReason: null,
  outstanding: 320,
}

const request: CardRequestDto = {
  membershipId: 'ms000000-0000-0000-0000-000000000001',
  customerId: prince.id,
  customerName: 'Prince',
  clubId: 'club-coffee',
  clubName: 'The Coffee Club',
  requestedAt: '2026-11-07T09:00:00Z',
}

/** Pending claims until one is resolved; the summary's count follows. */
function claimsServer() {
  let pending = [claim]
  const calls: { url: string; body: unknown }[] = []
  server.use(
    http.get(`${API}/payment-claims`, () => HttpResponse.json(pending)),
    http.get(`${API}/reports/summary`, () => HttpResponse.json({ ...summary, pendingClaimsCount: pending.length })),
    http.post(`${API}/payment-claims/:id/:action`, async ({ request: req, params }) => {
      calls.push({ url: `${params.id}/${params.action}`, body: await req.json() })
      pending = []
      return HttpResponse.json({ ...claim, status: params.action === 'confirm' ? 'Confirmed' : 'Rejected' })
    }),
  )
  return calls
}

describe('Payments to confirm', () => {
  it('lists a pending claim with its channel, reference and the tab now; Confirm settles it and the panel goes', async () => {
    const calls = claimsServer()
    const { user } = renderApp('/', signedIn)

    const panel = (await screen.findByRole('heading', { name: 'Payments to confirm' })).closest('div')!.parentElement!
    const row = within(panel).getByText('GC12345678').closest('tr')!
    expect(within(row).getByText('Kee')).toBeInTheDocument()
    expect(within(row).getByText('Business GCash')).toBeInTheDocument()
    expect(within(row).getByText('(GCash)')).toBeInTheDocument()
    expect(within(row).getAllByText('₱320')).toHaveLength(2)   // the claim, and the tab it's judged against

    // The sidebar badge counts it.
    expect(await screen.findByLabelText('1 waiting')).toBeInTheDocument()

    await user.click(within(row).getByRole('button', { name: 'Confirm' }))

    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Payments to confirm' })).not.toBeInTheDocument())
    expect(calls).toEqual([{ url: `${claim.id}/confirm`, body: {} }])
    expect(await screen.findByText('Confirmed ₱320 from Kee.')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByLabelText(/waiting/)).not.toBeInTheDocument())
  })

  it('Reject asks for a reason, then sends it', async () => {
    const calls = claimsServer()
    const { user } = renderApp('/', signedIn)

    await user.click(await screen.findByRole('button', { name: 'Reject' }))
    const dialog = screen.getByRole('dialog', { name: /Reject Kee's ₱320/ })
    await user.click(within(dialog).getByRole('button', { name: 'Reject' }))
    expect(within(dialog).getByText(/Say why/)).toBeInTheDocument()
    expect(calls).toHaveLength(0)

    await user.type(within(dialog).getByLabelText('Reason'), 'No transfer with this reference.')
    await user.click(within(dialog).getByRole('button', { name: 'Reject' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(calls).toEqual([{ url: `${claim.id}/reject`, body: { reason: 'No transfer with this reference.' } }])
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Payments to confirm' })).not.toBeInTheDocument())
  })

  it('a refused confirm (the tab shrank) keeps the claim and says why', async () => {
    server.use(
      http.get(`${API}/payment-claims`, () => HttpResponse.json([{ ...claim, outstanding: 100 }])),
      http.post(`${API}/payment-claims/:id/confirm`, () => problem(409, 'Conflict', 'That is more than the tab owes (₱100).')),
    )
    const { user } = renderApp('/', signedIn)

    expect(await screen.findByText('less than the claim')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Confirm' }))
    expect(await screen.findByText('That is more than the tab owes (₱100).')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Payments to confirm' })).toBeInTheDocument()
  })

  it('is hidden when nothing is waiting', async () => {
    renderApp('/', signedIn)
    await screen.findByRole('heading', { name: 'Outstanding tabs' })
    expect(screen.queryByRole('heading', { name: 'Payments to confirm' })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Card requests' })).not.toBeInTheDocument()
  })
})

describe('Card requests', () => {
  function requestsServer() {
    let waiting = [request]
    const calls: string[] = []
    server.use(
      http.get(`${API}/loyalty/card-requests`, () => HttpResponse.json(waiting)),
      http.get(`${API}/reports/summary`, () =>
        HttpResponse.json({ ...summary, pendingClaimsCount: 1, pendingCardRequestsCount: waiting.length }),
      ),
      http.post(`${API}/loyalty/memberships/:id/:action`, ({ params }) => {
        calls.push(`${params.id}/${params.action}`)
        waiting = []
        return new HttpResponse(null, { status: 204 })
      }),
    )
    return calls
  }

  it('Give card approves the request, and the badge counts requests with claims', async () => {
    const calls = requestsServer()
    const { user } = renderApp('/', signedIn)

    const panel = (await screen.findByRole('heading', { name: 'Card requests' })).closest('div')!.parentElement!
    expect(within(panel).getByText('Prince')).toBeInTheDocument()
    expect(within(panel).getByText('The Coffee Club')).toBeInTheDocument()
    expect(await screen.findByLabelText('2 waiting')).toBeInTheDocument()

    await user.click(within(panel).getByRole('button', { name: 'Give card' }))

    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Card requests' })).not.toBeInTheDocument())
    expect(calls).toEqual([`${request.membershipId}/approve`])
    expect(await screen.findByLabelText('1 waiting')).toBeInTheDocument()
  })

  it('Decline removes the request', async () => {
    const calls = requestsServer()
    const { user } = renderApp('/', signedIn)

    await user.click(await screen.findByRole('button', { name: 'Decline' }))
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Card requests' })).not.toBeInTheDocument())
    expect(calls).toEqual([`${request.membershipId}/decline`])
  })
})
