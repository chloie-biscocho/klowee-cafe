import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../../test/fixtures'
import { renderApp } from '../../../test/renderApp'
import { coffeeCard, coffeeClub, keeDetail } from '../../../test/salesFixtures'
import { server } from '../../../test/server'

describe('Customer loyalty cards', () => {
  it('draws the card: filled slots, the next one, rewards and drinks still to stamp', async () => {
    renderApp(`/sales/customers/${keeDetail.id}`, signedIn)

    const card = await screen.findByRole('region', { name: 'Coffee Club card' })
    expect(within(card).getByRole('list', { name: '4 of 12 slots filled' })).toBeInTheDocument()
    expect(within(card).getByLabelText('Slot 4, filled')).toBeInTheDocument()
    expect(within(card).getByLabelText('Slot 5, next, 50% OFF available')).toBeInTheDocument()
    expect(within(card).getByText('2 drinks on the tab will stamp once paid.')).toBeInTheDocument()
  })

  it('marks a reward used by hand, and makes a hand-marked one available again', async () => {
    let redeemed: unknown = null
    let unredeemed = ''
    server.use(
      http.get(`${API}/customers/:id`, () =>
        HttpResponse.json({ ...keeDetail, clubs: [coffeeCard(9, ['available', 'used', 'locked'])] }),
      ),
      http.post(`${API}/customers/:id/loyalty/redemptions`, async ({ request }) => {
        redeemed = await request.json()
        return HttpResponse.json({})
      }),
      http.delete(`${API}/customers/:id/loyalty/redemptions/:redemptionId`, ({ params }) => {
        unredeemed = String(params.redemptionId)
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { user } = renderApp(`/sales/customers/${keeDetail.id}`, signedIn)
    const card = await screen.findByRole('region', { name: 'Coffee Club card' })

    await user.click(within(card).getByRole('button', { name: 'Mark used' }))
    await waitFor(() => expect(redeemed).toMatchObject({ clubId: coffeeClub.id, rewardId: 'r-half', cycle: 0 }))

    await user.click(within(card).getByRole('button', { name: 'Make available' }))
    await waitFor(() => expect(unredeemed).toBe('red-r-shot'))
  })

  it('adjusts stamps on the card it was opened from', async () => {
    let sent: unknown = null
    server.use(
      http.post(`${API}/customers/:id/loyalty/adjust`, async ({ request }) => {
        sent = await request.json()
        return HttpResponse.json({})
      }),
    )
    const { user } = renderApp(`/sales/customers/${keeDetail.id}`, signedIn)
    const card = await screen.findByRole('region', { name: 'Coffee Club card' })
    await user.click(within(card).getByRole('button', { name: 'Adjust stamps' }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByLabelText('Card')).toHaveValue(coffeeClub.id)
    const stamps = within(dialog).getByLabelText('Stamps')
    await user.clear(stamps)
    await user.type(stamps, '3')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(sent).toEqual({ clubId: coffeeClub.id, delta: 3, notes: null }))
  })
})
