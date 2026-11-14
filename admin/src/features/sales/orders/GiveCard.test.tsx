import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../../test/fixtures'
import { todayInManila } from '../../../lib/dates'
import { renderApp } from '../../../test/renderApp'
import { coffeeCard, coffeeClub, kee, prince } from '../../../test/salesFixtures'
import { problem, server } from '../../../test/server'
import type { CustomerSummaryDto } from '../../../types/api'

const total = () => screen.getByLabelText('Total').textContent

/** A customer with no card, and the same customer once given one (0 of 12, joined today). */
const walkIn: CustomerSummaryDto = {
  ...prince,
  id: 'c-walk-in',
  name: 'Walk-in',
  clubs: [{ ...coffeeCard(0, ['locked', 'locked', 'locked']), membership: 'none', membershipId: null, rewards: [], cards: [] }],
}
const withCard = (joinedOn: string): CustomerSummaryDto => ({
  ...walkIn,
  clubs: [{ ...coffeeCard(0, ['locked', 'locked', 'locked']), joinedOn }],
})

/** Serves the customers list, swapping in the given card after the POST, and counts the POSTs. */
function serveWalkIn(today: string, fail = false) {
  const state = { posts: 0, given: false }
  server.use(
    http.get(`${API}/customers`, () => HttpResponse.json([kee, state.given ? withCard(today) : walkIn])),
    http.post(`${API}/customers/:id/loyalty/memberships`, async ({ request }) => {
      state.posts++
      expect(await request.json()).toEqual({ clubId: coffeeClub.id })
      if (fail) return problem(409, 'Conflict', 'The Coffee Club isn’t active.')
      state.given = true
      return HttpResponse.json({})
    }),
  )
  return state
}

async function choose(user: ReturnType<typeof renderApp>['user'], name: string) {
  const box = await screen.findByRole('combobox', { name: 'Customer' })
  await user.click(box)
  await user.type(box, `${name}{Enter}`)
}

const todayIso = () => todayInManila()

describe('Give card on the order form', () => {
  it('shows only for a customer without a card', async () => {
    serveWalkIn('2026-11-14')
    const { user } = renderApp('/sales/orders/new', signedIn)

    await choose(user, 'Kee')
    expect(screen.queryByRole('button', { name: 'Give card' })).not.toBeInTheDocument()

    const box = screen.getByRole('combobox', { name: 'Customer' })
    await user.clear(box)
    await user.type(box, 'Walk{Enter}')
    expect(await screen.findByText(/No loyalty card/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Give card' })).toBeInTheDocument()
  })

  it('gives the card once on confirm, keeps the unsaved lines, and the form then shows the card', async () => {
    const { user } = renderApp('/sales/orders/new', signedIn)
    const state = serveWalkIn(todayIso())
    await choose(user, 'Walk')
    await user.click(screen.getByRole('button', { name: 'One more Spanish Latte' }))
    await user.click(screen.getByRole('button', { name: 'One more Americano' }))
    expect(total()).toBe('₱260')

    await user.click(screen.getByRole('button', { name: 'Give card' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText("Give Walk-in a Coffee Club card? This order will be their first stamp once it's paid.")).toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: 'Give card' }))

    expect(await screen.findByText(/Coffee 0\/12/)).toBeInTheDocument()
    expect(state.posts).toBe(1)
    expect(screen.queryByText(/No loyalty card/)).not.toBeInTheDocument()
    expect(total()).toBe('₱260')                                                   // the lines survived
    expect(screen.getByLabelText('Spanish Latte quantity')).toHaveTextContent('1')
    expect(screen.queryByText(/dated before the card starts/)).not.toBeInTheDocument()
  })

  it('does nothing on cancel', async () => {
    const state = serveWalkIn('2026-11-14')
    const { user } = renderApp('/sales/orders/new', signedIn)
    await choose(user, 'Walk')

    await user.click(screen.getByRole('button', { name: 'Give card' }))
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancel' }))

    expect(state.posts).toBe(0)
    expect(screen.getByText(/No loyalty card/)).toBeInTheDocument()
  })

  it('says a backdated order won’t stamp a card that starts today', async () => {
    const { user } = renderApp('/sales/orders/new', signedIn)
    serveWalkIn(todayIso())
    await choose(user, 'Walk')
    const date = screen.getByLabelText('Date')
    await user.clear(date)
    await user.type(date, '2026-01-05')

    await user.click(screen.getByRole('button', { name: 'Give card' }))
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Give card' }))

    expect(await screen.findByText("This order is dated before the card starts, so it won't stamp.")).toBeInTheDocument()
  })

  it('shows a failure inline and leaves the form as it was', async () => {
    serveWalkIn('2026-11-14', true)
    const { user } = renderApp('/sales/orders/new', signedIn)
    await choose(user, 'Walk')
    await user.click(screen.getByRole('button', { name: 'One more Americano' }))

    await user.click(screen.getByRole('button', { name: 'Give card' }))
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Give card' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The Coffee Club isn’t active.')
    await waitFor(() => expect(total()).toBe('₱110'))
    expect(screen.getByText(/No loyalty card/)).toBeInTheDocument()
  })
})
