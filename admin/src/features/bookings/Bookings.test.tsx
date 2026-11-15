import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { summary } from '../../test/salesFixtures'
import { server } from '../../test/server'
import type { BookingDto, BookingStatus, BookingSummaryDto, SiteSettingRequest } from '../../types/api'

const request: BookingDto = {
  id: 'b0000000-0000-0000-0000-000000000001',
  name: 'Kee Santos',
  contact: '0917 555 0101',
  eventDate: '2026-12-12',
  venue: 'Limketkai Atrium',
  guestCount: 60,
  packageId: 'p-1',
  packageName: 'Starter',
  message: 'Office party, afternoon.',
  status: 'New',
  ownerNotes: null,
  statusChangedAt: null,
  statusChangedByName: null,
  createdAt: '2026-11-15T02:00:00Z',
}
const older: BookingSummaryDto = {
  id: 'b0000000-0000-0000-0000-000000000002', eventDate: '2027-01-09', name: 'Prince Lim', venue: 'Centrio', guestCount: null,
  packageName: null, status: 'Contacted', createdAt: '2026-11-10T02:00:00Z',
}
const summaryOf = (booking: BookingDto): BookingSummaryDto => ({
  id: booking.id, eventDate: booking.eventDate, name: booking.name, venue: booking.venue, guestCount: booking.guestCount,
  packageName: booking.packageName, status: booking.status, createdAt: booking.createdAt,
})

/** A tiny bookings backend: one New request and one Contacted, the summary counting New ones. */
function serveBookings() {
  let current = { ...request }
  const calls: { method: string; url: string; body?: unknown }[] = []
  const all = () => [summaryOf(current), older]
  server.use(
    http.get(`${API}/bookings`, ({ request: req }) => {
      const status = new URL(req.url).searchParams.get('status') as BookingStatus | null
      calls.push({ method: 'GET', url: req.url })
      const items = all().filter((b) => !status || b.status === status)
      return HttpResponse.json({ items, page: 1, pageSize: 20, totalCount: items.length })
    }),
    http.get(`${API}/bookings/:id`, () => HttpResponse.json(current)),
    http.patch(`${API}/bookings/:id`, async ({ request: req }) => {
      const body = (await req.json()) as { status: BookingStatus; ownerNotes: string | null }
      calls.push({ method: 'PATCH', url: req.url, body })
      current = {
        ...current,
        status: body.status,
        ownerNotes: body.ownerNotes,
        ...(body.status !== current.status ? { statusChangedAt: '2026-11-15T03:00:00Z', statusChangedByName: 'Kee' } : {}),
      }
      return HttpResponse.json(current)
    }),
    http.delete(`${API}/bookings/:id`, ({ request: req }) => {
      calls.push({ method: 'DELETE', url: req.url })
      return new HttpResponse(null, { status: 204 })
    }),
    http.get(`${API}/reports/summary`, () => HttpResponse.json({ ...summary, newBookingsCount: current.status === 'New' ? 1 : 0 })),
  )
  return calls
}

describe('Bookings', () => {
  it('opens on New, and the status filter changes the list', async () => {
    const calls = serveBookings()
    const { user } = renderApp('/site/bookings', signedIn)

    expect(await screen.findByRole('link', { name: 'Kee Santos' })).toHaveAttribute('href', `/site/bookings/${request.id}`)
    expect(screen.getByLabelText('Status')).toHaveValue('New')
    expect(screen.queryByText('Prince Lim')).not.toBeInTheDocument()
    expect(new URL(calls.at(-1)!.url).searchParams.get('status')).toBe('New')
    const row = screen.getByRole('link', { name: 'Kee Santos' }).closest('tr')!
    expect(within(row).getByText('Dec 12, 2026')).toBeInTheDocument()
    expect(within(row).getByText('Limketkai Atrium')).toBeInTheDocument()
    expect(within(row).getByText('60')).toBeInTheDocument()
    expect(within(row).getByText('Starter')).toBeInTheDocument()
    expect(within(row).getByText('New')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Status'), 'All')
    expect(await screen.findByText('Prince Lim')).toBeInTheDocument()
    expect(new URL(calls.at(-1)!.url).searchParams.has('status')).toBe(false)

    await user.selectOptions(screen.getByLabelText('Status'), 'Contacted')
    await waitFor(() => expect(screen.queryByText('Kee Santos')).not.toBeInTheDocument())
    expect(screen.getByText('Prince Lim')).toBeInTheDocument()
  })

  it('shows every field; a status change sends the status with the notes, and the badge and Dashboard count follow', async () => {
    const calls = serveBookings()
    const { user, router } = renderApp(`/site/bookings/${request.id}`, signedIn)

    expect(await screen.findByRole('heading', { name: 'Kee Santos' })).toBeInTheDocument()
    for (const text of ['Dec 12, 2026', '0917 555 0101', 'Limketkai Atrium', '60', 'Starter', 'Office party, afternoon.', 'Status not changed yet.'])
      expect(screen.getByText(text)).toBeInTheDocument()
    expect(await screen.findByLabelText('1 waiting')).toBeInTheDocument()
    // The current status is pressed, not greyed out; the others offer a change.
    expect(screen.getByRole('button', { name: 'New' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('button', { name: 'New' })).toBeEnabled()

    await user.type(screen.getByLabelText('Your notes'), 'Called, sent the menu.')
    await user.click(screen.getByRole('button', { name: 'Contacted' }))

    await waitFor(() => expect(calls.find((c) => c.method === 'PATCH')).toBeDefined())
    expect(calls.find((c) => c.method === 'PATCH')!.body).toEqual({ status: 'Contacted', ownerNotes: 'Called, sent the menu.' })
    expect(await screen.findByText(/Status changed .* by Kee\./)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Contacted' })).toHaveAttribute('aria-pressed', 'true')
    await waitFor(() => expect(screen.queryByLabelText('1 waiting')).not.toBeInTheDocument())

    // Back on the Dashboard, nothing New is left: no panel.
    await router.navigate('/')
    expect(await screen.findByRole('heading', { name: 'Revenue by month' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Booking requests' })).not.toBeInTheDocument()
  })

  it('saving notes alone sends the current status', async () => {
    const calls = serveBookings()
    const { user } = renderApp(`/site/bookings/${request.id}`, signedIn)

    await user.type(await screen.findByLabelText('Your notes'), 'Asked for a quote.')
    await user.click(screen.getByRole('button', { name: 'Save notes' }))

    await waitFor(() => expect(calls.find((c) => c.method === 'PATCH')?.body).toEqual({ status: 'New', ownerNotes: 'Asked for a quote.' }))
    expect(await screen.findByText('Notes saved.')).toBeInTheDocument()
  })

  it('copies the contact', async () => {
    serveBookings()
    const { user } = renderApp(`/site/bookings/${request.id}`, signedIn)

    await user.click(await screen.findByRole('button', { name: 'Copy' }))
    expect(await navigator.clipboard.readText()).toBe('0917 555 0101')
    expect(await screen.findByText('Contact copied.')).toBeInTheDocument()
  })

  it('deletes only after the confirm, then goes back to the list', async () => {
    const calls = serveBookings()
    const { user, router } = renderApp(`/site/bookings/${request.id}`, signedIn)

    await user.click(await screen.findByRole('button', { name: 'Delete' }))
    const dialog = screen.getByRole('dialog', { name: 'Delete booking request' })
    expect(within(dialog).getByText(/Delete Kee Santos's request\?/)).toBeInTheDocument()
    expect(calls.some((c) => c.method === 'DELETE')).toBe(false)

    await user.click(within(dialog).getByRole('button', { name: 'Delete' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/site/bookings'))
    expect(calls.filter((c) => c.method === 'DELETE').map((c) => new URL(c.url).pathname)).toEqual([`/api/bookings/${request.id}`])
  })

  it("Create event opens the New event form with the request's name, venue and date, status Planned", async () => {
    serveBookings()
    const { user, router } = renderApp(`/site/bookings/${request.id}`, signedIn)

    await user.click(await screen.findByRole('button', { name: 'Create event' }))

    const dialog = await screen.findByRole('dialog', { name: 'New event' })
    expect(router.state.location.pathname).toBe('/site/events')
    expect(router.state.location.search).toBe('')
    expect(within(dialog).getByLabelText('Name')).toHaveValue("Kee Santos's event")
    expect(within(dialog).getByLabelText('Venue')).toHaveValue('Limketkai Atrium')
    expect(within(dialog).getByLabelText('Starts on')).toHaveValue('2026-12-12')
    expect(within(dialog).getByLabelText('Ends on')).toHaveValue('2026-12-12')
    expect(within(dialog).getByLabelText('Status')).toHaveValue('Planned')

    // Cancel: the prefill is used up, so the form doesn't come back.
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(router.state.location.state).toBeNull()
  })

  it('after creating the event, Back does not reopen the prefilled form', async () => {
    serveBookings()
    server.use(http.post(`${API}/events`, async ({ request: req }) => {
      const body = (await req.json()) as Record<string, unknown>
      return HttpResponse.json({ ...body, id: 'e0000000-0000-0000-0000-0000000000aa', isPublished: false, photos: [], coverPhotoUrl: null }, { status: 201 })
    }))
    const { user, router } = renderApp(`/site/bookings/${request.id}`, signedIn)

    await user.click(await screen.findByRole('button', { name: 'Create event' }))
    const dialog = await screen.findByRole('dialog', { name: 'New event' })
    await user.click(within(dialog).getByRole('button', { name: 'Create event' }))
    await waitFor(() => expect(router.state.location.pathname).toBe('/site/events/e0000000-0000-0000-0000-0000000000aa'))

    await router.navigate(-1)
    await waitFor(() => expect(router.state.location.pathname).toBe('/site/events'))
    expect(router.state.location.state).toBeNull()
    expect(screen.queryByRole('dialog', { name: 'New event' })).not.toBeInTheDocument()
  })

  it('the Dashboard lists New requests with a link to each', async () => {
    serveBookings()
    renderApp('/', signedIn)

    const panel = (await screen.findByRole('heading', { name: 'Booking requests' })).closest('section, div')!.parentElement!
    expect(within(panel).getByText('Kee Santos')).toBeInTheDocument()
    expect(within(panel).getByText('Dec 12, 2026')).toBeInTheDocument()
    expect(within(panel).getByRole('link', { name: "Open Kee Santos's request" })).toHaveAttribute('href', `/site/bookings/${request.id}`)
    expect(within(panel).queryByText('Prince Lim')).not.toBeInTheDocument()
  })

  it('the sidebar links to Bookings', async () => {
    serveBookings()
    renderApp('/', signedIn)
    expect(await screen.findByRole('link', { name: 'Bookings' })).toHaveAttribute('href', '/site/bookings')
  })
})

describe('Settings → Bookings', () => {
  it('saves the switch and the note together', async () => {
    let saved: SiteSettingRequest[] | null = null
    server.use(
      http.put(`${API}/settings`, async ({ request: req }) => {
        saved = (await req.json()) as SiteSettingRequest[]
        return HttpResponse.json([])
      }),
    )
    const { user } = renderApp('/site/settings', signedIn)

    const toggle = await screen.findByRole('switch', { name: /Booking form on the website/ })
    expect(toggle).toBeChecked()
    expect(screen.getByRole('button', { name: 'Save bookings' })).toBeDisabled()

    await user.click(toggle)
    expect(screen.getByText('The Book section shows your contact buttons only.')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Note above the form'), 'Two weeks ahead, please.')
    await user.click(screen.getByRole('button', { name: 'Save bookings' }))

    await waitFor(() =>
      expect(saved).toEqual([
        { key: 'booking_form_enabled', value: 'false' },
        { key: 'booking_form_note', value: 'Two weeks ahead, please.' },
      ]),
    )
    expect(await screen.findByText('Booking form is off.')).toBeInTheDocument()
  })

  it('reads a saved "false" as off', async () => {
    server.use(http.get(`${API}/settings`, () => HttpResponse.json([{ key: 'booking_form_enabled', value: 'false' }])))
    renderApp('/site/settings', signedIn)
    expect(await screen.findByRole('switch', { name: /Booking form on the website/ })).not.toBeChecked()
  })
})
