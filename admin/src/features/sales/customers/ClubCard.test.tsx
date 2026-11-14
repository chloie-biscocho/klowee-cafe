import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../../test/fixtures'
import { renderApp } from '../../../test/renderApp'
import { coffeeCard, coffeeClub, kee, keeDetail, prince, stackOf } from '../../../test/salesFixtures'
import { server } from '../../../test/server'

describe('Customer loyalty cards', () => {
  it('draws the card: two rows of six, stamped slots, reward slots, drinks still to stamp', async () => {
    renderApp(`/sales/customers/${keeDetail.id}`, signedIn)

    const card = await screen.findByRole('region', { name: 'Coffee Club card' })
    expect(within(card).getByText('card 1, 4 of 12')).toBeInTheDocument()
    const slots = within(card).getByRole('list', { name: 'Card 1: 4 of 12 slots stamped' })
    expect(within(slots).getAllByRole('listitem')).toHaveLength(12)
    expect(within(card).getByLabelText('Slot 4, stamped, added by hand')).toHaveAttribute('title', 'Added by hand')
    expect(within(card).getByLabelText('Slot 5, empty, 50% OFF available')).toBeInTheDocument()
    expect(within(card).getByLabelText('Slot 12, empty, FREE DRINK locked')).toHaveTextContent('free drink')
    expect(within(card).getByText('2 drinks on the tab will stamp once paid.')).toBeInTheDocument()
    expect(within(card).queryByText(/Card \d of/)).not.toBeInTheDocument()      // one card: no stack, no counter
    expect(within(card).queryByRole('button', { name: /Show card/ })).not.toBeInTheDocument()
  })

  it('shows dated order stamps as links, the newest card first, and earlier cards behind it', async () => {
    const stamps = (overall: number) =>
      overall === 25
        ? { date: '2026-09-02', orderId: 'o-sep2', source: 'order' as const }
        : { date: null, orderId: null, source: 'carriedOver' as const }
    server.use(
      http.get(`${API}/customers/:id`, () =>
        HttpResponse.json({ ...keeDetail, clubs: [{ ...coffeeCard(1, ['locked', 'locked', 'locked']), cycle: 2, position: 1, cards: stackOf(2, 1, stamps) }] }),
      ),
    )
    const { user } = renderApp(`/sales/customers/${keeDetail.id}`, signedIn)
    const card = await screen.findByRole('region', { name: 'Coffee Club card' })

    // The current card opens first, its stamp dated 9/2 and linked to its order.
    expect(within(card).getByText('Card 3 of 3')).toBeInTheDocument()
    expect(within(card).getByRole('link', { name: 'Slot 1, stamped 2 September' })).toHaveAttribute('href', '/sales/orders/o-sep2')
    expect(within(card).getByText('9/2')).toBeInTheDocument()
    expect(within(card).getByRole('button', { name: 'Adjust stamps' })).toBeInTheDocument()

    // A card behind comes to the front by click; the arrows walk the stack.
    await user.click(within(card).getByRole('button', { name: 'Show card 1' }))
    expect(within(card).getByText('Card 1 of 3')).toBeInTheDocument()
    expect(within(card).getByText('from paper card')).toBeInTheDocument()
    expect(within(card).getByLabelText('Slot 2, stamped, from the paper card')).toHaveAttribute('title', 'From the paper card')
    expect(within(card).queryByRole('button', { name: 'Adjust stamps' })).not.toBeInTheDocument()
    expect(within(card).getByRole('button', { name: 'Previous card' })).toBeDisabled()

    await user.click(within(card).getByRole('button', { name: 'Next card' }))
    expect(within(card).getByText('Card 2 of 3')).toBeInTheDocument()
  })

  it('reads which card a carried-over cardholder is on', async () => {
    server.use(
      http.get(`${API}/customers/:id`, () =>
        HttpResponse.json({ ...keeDetail, clubs: [{ ...coffeeCard(7, ['used', 'available', 'locked']), cycle: 3, cards: stackOf(3, 7) }] }),
      ),
    )
    renderApp(`/sales/customers/${keeDetail.id}`, signedIn)

    const card = await screen.findByRole('region', { name: 'Coffee Club card' })
    expect(within(card).getByText('card 4, 7 of 12')).toBeInTheDocument()
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
