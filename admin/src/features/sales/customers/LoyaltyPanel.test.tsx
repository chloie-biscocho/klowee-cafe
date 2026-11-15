import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../../test/fixtures'
import { renderApp } from '../../../test/renderApp'
import { coffeeCard, keeDetail, matchaClub, stackOf } from '../../../test/salesFixtures'
import { server } from '../../../test/server'
import type { CustomerClubDto, LoyaltyEntryDto } from '../../../types/api'

const showKee = (clubs: CustomerClubDto[], ledger: LoyaltyEntryDto[] = []) =>
  server.use(
    http.get(`${API}/customers/:id`, () => HttpResponse.json({ ...keeDetail, clubs })),
    http.get(`${API}/customers/:id/loyalty`, () => HttpResponse.json(ledger)),
  )

describe('the loyalty cards panel (handover 18a)', () => {
  it('one club: the details say where they are, list the rewards with states and actions, and there are no tabs', async () => {
    renderApp(`/sales/customers/${keeDetail.id}`, signedIn)
    const card = await screen.findByRole('region', { name: 'Coffee Club card' })

    expect(within(card).getByText('Card 1 · 4 of 12')).toBeInTheDocument()
    expect(within(card).getByText('50% off is ready to use.')).toBeInTheDocument()
    expect(screen.queryByRole('tablist', { name: 'Loyalty cards' })).not.toBeInTheDocument()   // the page's Orders/Ledger tabs are another list

    const rewards = within(card).getByRole('heading', { name: 'Rewards on card 1' }).nextElementSibling as HTMLElement
    const rows = within(rewards).getAllByRole('listitem')
    expect(rows.map((r) => r.textContent)).toEqual([
      expect.stringMatching(/^50% OFFslot 5ready.*Mark used$/),
      expect.stringMatching(/^EXTRA SHOTslot 8locked$/),
      expect.stringMatching(/^FREE DRINKslot 12locked$/),
    ])
    // The actions now sit in the details column.
    expect(within(card).getByRole('button', { name: 'Adjust stamps' })).toBeInTheDocument()
    expect(within(card).getByRole('button', { name: 'Take card away' })).toBeInTheDocument()
  })

  it('says what comes next when nothing is ready', async () => {
    showKee([coffeeCard(2, ['locked', 'locked', 'locked'])])
    renderApp(`/sales/customers/${keeDetail.id}`, signedIn)
    const card = await screen.findByRole('region', { name: 'Coffee Club card' })
    expect(within(card).getByText('Next: 50% off in 3 more drinks.')).toBeInTheDocument()
  })

  it('two clubs: tabs carry the positions and switch the card and its details', async () => {
    const coffee = coffeeCard(4, ['available', 'locked', 'locked'])
    const matcha: CustomerClubDto = { ...coffeeCard(2, ['locked', 'locked', 'locked']), clubId: matchaClub.id, name: matchaClub.name, style: 'Matcha' }
    showKee([coffee, matcha])
    const { user } = renderApp(`/sales/customers/${keeDetail.id}`, signedIn)

    const tabs = within(await screen.findByRole('tablist', { name: 'Loyalty cards' })).getAllByRole('tab')
    expect(tabs.map((t) => t.textContent)).toEqual(['Coffee Club 4/12', 'Matcha Club 2/12'])
    expect(tabs[0]).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('region', { name: 'Coffee Club card' })).toBeInTheDocument()

    await user.click(tabs[1]!)
    const card = screen.getByRole('region', { name: 'Matcha Club card' })
    expect(within(card).getByText('Card 1 · 2 of 12')).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'Coffee Club card' })).not.toBeInTheDocument()

    // Arrow keys move between tabs.
    await user.keyboard('{ArrowLeft}')
    expect(screen.getByRole('tab', { name: 'Coffee Club 4/12' })).toHaveAttribute('aria-selected', 'true')
  })

  it('a club without a card is a tab with no count', async () => {
    showKee([coffeeCard(4, ['available', 'locked', 'locked']), { ...coffeeCard(0, ['locked', 'locked', 'locked']), clubId: matchaClub.id, name: matchaClub.name, membership: 'none', membershipId: null, rewards: [], cards: [] }])
    const { user } = renderApp(`/sales/customers/${keeDetail.id}`, signedIn)
    await user.click(await screen.findByRole('tab', { name: 'Matcha Club' }))
    expect(screen.getByRole('button', { name: 'Give card' })).toBeInTheDocument()
  })

  it('Mark used and Make available still call the same endpoints from their new place', async () => {
    const calls: string[] = []
    showKee([coffeeCard(11, ['used', 'available', 'available'])])
    server.use(
      http.post(`${API}/customers/:id/loyalty/redemptions`, ({ request }) => {
        calls.push(`POST ${new URL(request.url).pathname}`)
        return HttpResponse.json({})
      }),
      http.delete(`${API}/customers/:id/loyalty/redemptions/:redemptionId`, ({ request }) => {
        calls.push(`DELETE ${new URL(request.url).pathname}`)
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { user } = renderApp(`/sales/customers/${keeDetail.id}`, signedIn)
    const card = await screen.findByRole('region', { name: 'Coffee Club card' })

    await user.click(within(card).getAllByRole('button', { name: 'Mark used' })[0]!)
    await user.click(within(card).getByRole('button', { name: 'Make available' }))
    await waitFor(() => expect(calls).toHaveLength(2))
    expect(calls).toEqual([`POST /api/customers/${keeDetail.id}/loyalty/redemptions`, `DELETE /api/customers/${keeDetail.id}/loyalty/redemptions/red-r-half`])
  })

  it('recent stamps: newest first, an order link for a dated stamp, the paper card, and a hand stamp’s reason', async () => {
    // Card 1: slots 1 to 3 from the paper card, slot 4 by hand, slot 5 from an order.
    const source = (overall: number) =>
      overall <= 3
        ? { date: null, orderId: null, source: 'carriedOver' as const }
        : overall === 4
          ? { date: null, orderId: null, source: 'adjustment' as const }
          : { date: '2026-11-03', orderId: 'o-nov3', source: 'order' as const }
    const club = { ...coffeeCard(5, ['locked', 'locked', 'locked']), cards: stackOf(0, 5, source) }
    const entry = (over: Partial<LoyaltyEntryDto>): LoyaltyEntryDto => ({
      id: 'e', clubId: club.clubId, clubName: club.name, delta: 1, reason: 'Adjustment', orderId: null, notes: null, createdAt: '2026-11-01T00:00:00Z', ...over,
    })
    showKee([club], [
      entry({ id: 'e3', reason: 'Earned', orderId: 'o-nov3', createdAt: '2026-11-03T00:00:00Z' }),
      entry({ id: 'e2', notes: 'stamp card found in the cart', createdAt: '2026-11-02T00:00:00Z' }),
      entry({ id: 'e1', delta: 3, notes: 'carried over from paper card' }),
    ])
    renderApp(`/sales/customers/${keeDetail.id}`, signedIn)
    const card = await screen.findByRole('region', { name: 'Coffee Club card' })

    const list = within(card).getByRole('list', { name: 'Recent stamps' })
    await waitFor(() => expect(within(list).getAllByRole('listitem')[1]).toHaveTextContent('added by hand: stamp card found in the cart'))
    const rows = within(list).getAllByRole('listitem')
    expect(rows).toHaveLength(3)
    expect(within(rows[0]!).getByRole('link', { name: 'order' })).toHaveAttribute('href', '/sales/orders/o-nov3')
    expect(rows[0]).toHaveTextContent('Nov 3, 2026')
    expect(rows[2]).toHaveTextContent('from the paper card')
  })
})
