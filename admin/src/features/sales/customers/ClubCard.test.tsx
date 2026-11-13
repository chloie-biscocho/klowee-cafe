import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../../test/fixtures'
import { renderApp } from '../../../test/renderApp'
import { coffeeCard, coffeeClub, kee, keeDetail, prince } from '../../../test/salesFixtures'
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

  it('gives a card to a customer without one', async () => {
    let given: unknown = null
    server.use(
      http.get(`${API}/customers/:id`, () =>
        HttpResponse.json({ ...keeDetail, clubs: [{ ...coffeeCard(0, ['locked', 'locked', 'locked']), membership: 'none', membershipId: null, rewards: [] }] }),
      ),
      http.post(`${API}/customers/:id/loyalty/memberships`, async ({ request }) => {
        given = await request.json()
        return HttpResponse.json({})
      }),
    )
    const { user } = renderApp(`/sales/customers/${keeDetail.id}`, signedIn)

    const card = await screen.findByRole('region', { name: 'Coffee Club card' })
    expect(within(card).getByText(/No Coffee Club card/)).toBeInTheDocument()
    expect(within(card).queryByRole('button', { name: 'Adjust stamps' })).not.toBeInTheDocument()
    await user.click(within(card).getByRole('button', { name: 'Give card' }))

    await waitFor(() => expect(given).toEqual({ clubId: coffeeClub.id }))
  })

  it('takes a card away after saying its stamps go with it', async () => {
    let url = ''
    server.use(
      http.delete(`${API}/customers/:id/loyalty/memberships/:membershipId`, ({ request }) => {
        url = request.url
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { user } = renderApp(`/sales/customers/${keeDetail.id}`, signedIn)
    const card = await screen.findByRole('region', { name: 'Coffee Club card' })
    await user.click(within(card).getByRole('button', { name: 'Take card away' }))

    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/Its 4 stamps are voided/)).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Take card away' }))

    await waitFor(() => expect(url).toMatch(/\/loyalty\/memberships\/mem-coffee\?force=true$/))
  })

  it('shows "no card" in the list, filters to cardholders, and says so in the order form', async () => {
    const walkIn = { ...prince, id: 'c-walk-in', name: 'Walk-in', clubs: [{ ...coffeeCard(0, ['locked', 'locked', 'locked']), membership: 'none' as const, membershipId: null, rewards: [] }] }
    server.use(http.get(`${API}/customers`, () => HttpResponse.json([kee, walkIn])))
    const { user } = renderApp('/sales/customers', signedIn)

    const row = (await screen.findByText('Walk-in')).closest('tr')!
    expect(within(row).getByText('no card')).toBeInTheDocument()
    await user.click(screen.getByLabelText('Has a card'))
    expect(screen.queryByText('Walk-in')).not.toBeInTheDocument()
    expect(screen.getByText('Kee')).toBeInTheDocument()
  })

  it('reads "No loyalty card" under a customer without one on the order form', async () => {
    const walkIn = { ...prince, id: 'c-walk-in', name: 'Walk-in', clubs: [{ ...coffeeCard(0, ['locked', 'locked', 'locked']), membership: 'none' as const, membershipId: null, rewards: [] }] }
    server.use(http.get(`${API}/customers`, () => HttpResponse.json([kee, walkIn])))
    const { user } = renderApp('/sales/orders/new', signedIn)
    const box = await screen.findByRole('combobox', { name: 'Customer' })
    await user.click(box)
    await user.type(box, 'Walk{Enter}')

    expect(await screen.findByText(/No loyalty card/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'One more Americano' }))
    expect(within(screen.getByRole('listitem', { name: 'Americano' })).queryByRole('switch')).not.toBeInTheDocument()
  })
})
