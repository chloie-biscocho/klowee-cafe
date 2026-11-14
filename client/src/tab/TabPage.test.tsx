import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { Root } from '../Root'
import { server } from '../test/server'
import { card, cimb, coffeeMember, gcash, matchaNone, tab, TAB_URL, TOKEN } from '../test/tabFixtures'
import type { ClaimRequest, PublicTab } from './types'

function serveTab(body: PublicTab) {
  server.use(http.get(TAB_URL, () => HttpResponse.json(body)))
}

async function renderTab(body: PublicTab = tab) {
  serveTab(body)
  const user = userEvent.setup()
  render(<Root path={`/tab/${TOKEN}`} />)
  await screen.findByRole('heading', { name: 'your tab' })
  return user
}

const receipt = () => screen.getByRole('heading', { name: 'your tab' }).closest('section')!

describe('the tab page: receipt', () => {
  it('lists each unpaid order with what is due, a partly-paid line, and the total', async () => {
    await renderTab()

    expect(screen.getByText('hi, Dean')).toBeInTheDocument()
    const rows = within(receipt()).getAllByRole('listitem')
    expect(rows).toHaveLength(3)
    expect(rows[0]).toHaveTextContent('Oat Biscoff Latte ×2')
    expect(rows[0]).toHaveTextContent('oct 21 · 200 paid')
    expect(rows[0]).toHaveTextContent('180')
    expect(rows[1]).toHaveTextContent('nov 11')
    expect(rows[1]).not.toHaveTextContent('paid')
    expect(within(receipt()).getByText('you owe').parentElement).toHaveTextContent('₱780')
    expect(within(receipt()).getByRole('button', { name: 'Pay with GCash' })).toBeInTheDocument()

    expect(screen.getByRole('heading', { name: 'Recent payments' }).closest('section')).toHaveTextContent('Cash')
    expect(screen.getByText("This page is only for you. Don't share the link.")).toBeInTheDocument()
    expect(document.querySelector('meta[name="robots"]')).toHaveAttribute('content', 'noindex')
  })

  it('all paid up: no rows, no Pay', async () => {
    await renderTab({ ...tab, balance: 0, unpaidOrders: [] })
    expect(within(receipt()).getByText("you're all paid up")).toBeInTheDocument()
    expect(within(receipt()).queryByRole('button')).not.toBeInTheDocument()
  })

  it('with no way to pay set up, hides Pay and says to pay in person', async () => {
    await renderTab({ ...tab, pay: { note: null, methods: [] } })
    expect(within(receipt()).queryByRole('button', { name: /Pay/ })).not.toBeInTheDocument()
    expect(within(receipt()).getByText(/see Klowee in person/)).toBeInTheDocument()
  })

  it('shows a rejected report with its reason', async () => {
    await renderTab({
      ...tab,
      claims: [{ id: 'c1', amount: 780, reference: 'GC12345678', status: 'Rejected', createdOn: '2026-11-13', channel: 'GCash', rejectReason: 'No transfer with this reference.' }],
    })
    expect(within(receipt()).getByText(/couldn't confirm your ₱780 by GCash \(ref GC12345678\): No transfer with this reference\./)).toBeInTheDocument()
    expect(within(receipt()).getByRole('button', { name: 'Pay with GCash' })).toBeInTheDocument()
  })
})

describe('the tab page: Pay', () => {
  it('with two methods, a control switches the QR and the account', async () => {
    const user = await renderTab({ ...tab, pay: { note: 'Exact amount, please.', methods: [gcash, cimb] } })

    await user.click(within(receipt()).getByRole('button', { name: 'Pay' }))
    const dialog = screen.getByRole('dialog', { name: 'Pay ₱780' })
    expect(within(dialog).getByText('Exact amount, please.')).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: 'GCash', pressed: true })).toBeInTheDocument()
    expect(within(dialog).getByRole('img', { name: /GCash QR code/ })).toHaveAttribute('src', gcash.qrUrl)
    expect(within(dialog).getByText('0917 123 4567')).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'CIMB Bank' }))
    expect(within(dialog).getByRole('img', { name: /CIMB Bank QR code/ })).toHaveAttribute('src', cimb.qrUrl)
    expect(within(dialog).getByText('Chloie B.')).toBeInTheDocument()
    expect(within(dialog).getByText('8001 2345 67')).toBeInTheDocument()
    expect(within(dialog).getByRole('link', { name: 'Save QR' })).toHaveAttribute('href', cimb.qrUrl)
    expect(within(dialog).getByText(/choose Upload QR in your banking app/)).toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Not yet' }))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('with one method, shows no control; Copy copies the number', async () => {
    const user = await renderTab()

    await user.click(within(receipt()).getByRole('button', { name: 'Pay with GCash' }))
    const dialog = screen.getByRole('dialog')
    expect(within(dialog).queryByRole('group', { name: 'Pay with' })).not.toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Copy' }))
    expect(await navigator.clipboard.readText()).toBe('0917 123 4567')
    expect(within(dialog).getByText('Copied')).toBeInTheDocument()
  })

  it('checks the reference, sends the chosen method and a lowered amount, then shows the waiting line', async () => {
    let sent: ClaimRequest | null = null
    let reported = false
    server.use(
      http.get(TAB_URL, () =>
        HttpResponse.json(
          reported
            ? {
                ...tab,
                pendingClaimsTotal: 500,
                claims: [{ id: 'c2', amount: 500, reference: 'CIMB778899', status: 'Pending', createdOn: '2026-11-14', channel: 'CIMB', rejectReason: null }],
                pay: { note: null, methods: [gcash, cimb] },
              }
            : { ...tab, pay: { note: null, methods: [gcash, cimb] } },
        ),
      ),
      http.post(`${TAB_URL}/claims`, async ({ request }) => {
        sent = (await request.json()) as ClaimRequest
        reported = true
        return HttpResponse.json({ id: 'c2', amount: 500, reference: 'CIMB778899', status: 'Pending', createdOn: '2026-11-14', channel: 'CIMB', rejectReason: null })
      }),
    )
    const user = userEvent.setup()
    render(<Root path={`/tab/${TOKEN}`} />)

    await user.click(await screen.findByRole('button', { name: 'Pay' }))
    const dialog = screen.getByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'CIMB Bank' }))
    const amount = within(dialog).getByLabelText('Amount sent')
    expect(amount).toHaveValue(780)

    // A bad reference is caught before anything is sent.
    await user.type(within(dialog).getByLabelText('CIMB Bank reference number'), 'ab-12')
    await user.click(within(dialog).getByRole('button', { name: "I've paid" }))
    expect(within(dialog).getByRole('alert')).toHaveTextContent('6 to 32 letters and digits')
    // So is more than the tab.
    await user.clear(amount)
    await user.type(amount, '800')
    await user.click(within(dialog).getByRole('button', { name: "I've paid" }))
    expect(within(dialog).getByRole('alert')).toHaveTextContent("That's more than your tab")
    expect(sent).toBeNull()

    await user.clear(amount)
    await user.type(amount, '500')
    await user.clear(within(dialog).getByLabelText('CIMB Bank reference number'))
    await user.type(within(dialog).getByLabelText('CIMB Bank reference number'), 'CIMB 7788 99')
    await user.click(within(dialog).getByRole('button', { name: "I've paid" }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(sent).toEqual({ amount: 500, reference: 'CIMB778899', paymentMethodId: cimb.id })
    expect(await within(receipt()).findByText('₱500 reported as paid by CIMB. Waiting for Klowee to confirm.')).toBeInTheDocument()
    // ₱280 is still unreported, so Pay stays.
    expect(within(receipt()).getByRole('button', { name: 'Pay' })).toBeInTheDocument()
  })

  it('a report that covers the whole tab replaces Pay with the waiting line; the API refusing shows its words', async () => {
    server.use(http.post(`${TAB_URL}/claims`, () => HttpResponse.json({ status: 409, detail: 'Your tab is already covered.' }, { status: 409 })))
    const user = await renderTab()

    await user.click(within(receipt()).getByRole('button', { name: 'Pay with GCash' }))
    const dialog = screen.getByRole('dialog')
    await user.type(within(dialog).getByLabelText('GCash reference number'), '1234567890123')
    await user.click(within(dialog).getByRole('button', { name: "I've paid" }))
    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Your tab is already covered.')
  })

  it('a fully reported tab shows the waiting state instead of Pay', async () => {
    await renderTab({
      ...tab,
      pendingClaimsTotal: 780,
      claims: [{ id: 'c3', amount: 780, reference: 'GC99887766', status: 'Pending', createdOn: '2026-11-14', channel: 'BusinessGCash', rejectReason: null }],
    })
    expect(within(receipt()).getByText('₱780 reported as paid by GCash. Waiting for Klowee to confirm.')).toBeInTheDocument()
    expect(within(receipt()).queryByRole('button')).not.toBeInTheDocument()
  })
})

describe('the tab page: loyalty cards', () => {
  const cards = () => screen.getByRole('heading', { name: 'Your loyalty cards' }).closest('section')!

  it('a member sees the live card: dated, stamped, with the sentence from the data', async () => {
    await renderTab()

    const tabs = within(cards()).getAllByRole('tab')
    expect(tabs.map((t) => t.textContent)).toEqual(['Coffee Club 6/12', 'Matcha Club'])
    expect(tabs[0]).toHaveAttribute('aria-selected', 'true')

    const live = within(cards()).getByRole('group', { name: /Coffee Club, card 1: 6 of 12 stamped/ })
    expect(live.querySelectorAll('[data-stamp="SIPPED"]')).toHaveLength(5)
    expect(live.querySelectorAll('[data-stamp="CLAIMED"]')).toHaveLength(1)   // slot 5, the 50% off used
    expect(live).toHaveAccessibleName(/slot 5 \(50% off, used\): stamped October 14/)
    expect(within(live).getByText('10/10')).toBeInTheDocument()      // the first stamp's date, M/D
    expect(cards()).toHaveTextContent('6 of 12. 50% off used.')
    expect(cards()).toHaveTextContent('Next up: extra shot in 2 more drinks.')
    expect(cards()).toHaveTextContent('2 drinks on your tab will stamp your card once paid.')
  })

  it('tabs switch cards; a non-member sees the dimmed card and asks for one', async () => {
    let asked = false
    const posted: string[] = []
    server.use(
      http.get(TAB_URL, () =>
        HttpResponse.json({ ...tab, clubs: [coffeeMember, asked ? { ...matchaNone, membership: 'requested', requestedOn: '2026-11-13' } : matchaNone] }),
      ),
      http.post(`${TAB_URL}/clubs/:clubId/request`, ({ params }) => {
        posted.push(params.clubId as string)
        asked = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const user = userEvent.setup()
    render(<Root path={`/tab/${TOKEN}`} />)

    await user.click(await screen.findByRole('tab', { name: 'Matcha Club' }))
    expect(screen.getByRole('tab', { name: 'Matcha Club' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('img', { name: /Matcha Club card, not yet yours. Its rewards: 50% off on slot 5, free topping on slot 8/ })).toBeInTheDocument()
    expect(cards().querySelector('[data-stamp]')).toBeNull()
    expect(within(cards()).getByText("You don't have a Matcha Club card yet.")).toBeInTheDocument()

    await user.click(within(cards()).getByRole('button', { name: 'Ask for a card' }))

    expect(await within(cards()).findByText('Asked on Nov 13. Klowee will set it up.')).toBeInTheDocument()
    expect(posted).toEqual(['club-matcha'])
    expect(within(cards()).queryByRole('button', { name: /Ask for/ })).not.toBeInTheDocument()

    // Arrow keys move between tabs.
    screen.getByRole('tab', { name: 'Matcha Club' }).focus()
    await user.keyboard('{ArrowLeft}')
    expect(screen.getByRole('tab', { name: 'Coffee Club 6/12' })).toHaveAttribute('aria-selected', 'true')
  })

  it('one club: no tabs', async () => {
    await renderTab({ ...tab, clubs: [coffeeMember] })
    expect(within(cards()).queryByRole('tablist')).not.toBeInTheDocument()
    expect(within(cards()).getByRole('group', { name: /Coffee Club, card 1/ })).toBeInTheDocument()
  })

  it('a card that comes with another asks for that one', async () => {
    const coffeeNone = { ...coffeeMember, membership: 'none' as const, position: 0, pendingDrinks: 0, cards: [] }
    const matchaAuto = { ...matchaNone, comesWithClubId: 'club-coffee', comesWithName: 'The Coffee Club' }
    const posted: string[] = []
    server.use(http.post(`${TAB_URL}/clubs/:clubId/request`, ({ params }) => {
      posted.push(params.clubId as string)
      return new HttpResponse(null, { status: 204 })
    }))
    const user = await renderTab({ ...tab, clubs: [coffeeNone, matchaAuto] })

    await user.click(screen.getByRole('tab', { name: 'Matcha Club' }))
    expect(within(cards()).getByText('Comes with your Coffee Club card.')).toBeInTheDocument()
    await user.click(within(cards()).getByRole('button', { name: 'Ask for a Coffee Club card' }))
    await waitFor(() => expect(posted).toEqual(['club-coffee']))
  })

  it('on card 3, opens on the current card; earlier cards are behind, with a leftover reward called out', async () => {
    const stacked = {
      ...coffeeMember,
      position: 2,
      cycle: 2,
      pendingDrinks: 0,
      rewards: [],
      cards: [
        card(1, 12, ['used', 'used', 'available'], false),
        card(2, 12, ['used', 'used', 'used'], false),
        card(3, 2, ['locked', 'locked', 'locked'], true),
      ],
    }
    const user = await renderTab({ ...tab, clubs: [stacked] })

    expect(within(cards()).getByText('Card 3 of 3')).toBeInTheDocument()
    expect(within(cards()).getByRole('button', { name: 'Next card' })).toBeDisabled()
    expect(cards()).toHaveTextContent('Still to use from card 1: free drink.')

    await user.click(within(cards()).getByRole('button', { name: 'Previous card' }))
    await user.click(within(cards()).getByRole('button', { name: 'Previous card' }))
    expect(within(cards()).getByText('Card 1 of 3')).toBeInTheDocument()
    expect(within(cards()).getByRole('group', { name: /card 1: 12 of 12 stamped/ })).toBeInTheDocument()
    expect(cards()).toHaveTextContent('Card 1, finished. Free drink still to use.')
  })
})

describe('the tab page: links and errors', () => {
  it('a dead link says to ask for a new one', async () => {
    server.use(http.get(TAB_URL, () => HttpResponse.json({ status: 404, detail: "This link isn't active." }, { status: 404 })))
    render(<Root path={`/tab/${TOKEN}`} />)
    expect(await screen.findByRole('heading', { name: "this link isn't active anymore" })).toBeInTheDocument()
    expect(screen.getByText(/Ask Klowee for a new one/)).toBeInTheDocument()
  })

  it('a network error shows the error card, and retry loads the page', async () => {
    let fail = true
    server.use(http.get(TAB_URL, () => (fail ? HttpResponse.error() : HttpResponse.json(tab))))
    const user = userEvent.setup()
    render(<Root path={`/tab/${TOKEN}`} />)

    const retry = await screen.findByRole('button', { name: 'try again' })
    fail = false
    await user.click(retry)
    expect(await screen.findByRole('heading', { name: 'your tab' })).toBeInTheDocument()
  })

  it('the homepage still renders on /', async () => {
    render(<Root path="/" />)
    expect(await screen.findByRole('link', { name: 'Klowee Cafe home' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'your tab' })).not.toBeInTheDocument()
  })
})
