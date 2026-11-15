import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { App } from '../App'
import type { PublicHome } from '../api/types'
import { BOOKINGS_URL, businessToday, latestEventDate, validateBooking, EMPTY_BOOKING } from '../lib/booking'
import { Book } from '../sections/Book'
import { fullHome } from '../test/fixtures'
import { serveHome, server } from '../test/server'

const home: PublicHome = {
  ...fullHome,
  packages: [...fullHome.packages, { id: 'p-2', name: 'Regular', price: 10500, description: '', guestCountNote: '', inclusions: [] }],
}

/** The form, once its lazy chunk has loaded. */
async function form() {
  return screen.findByRole('form', { name: 'Booking request' })
}

async function fillValid(user: ReturnType<typeof userEvent.setup>, f: HTMLElement) {
  await user.type(within(f).getByLabelText('Your name'), 'Kee Santos')
  await user.type(within(f).getByLabelText('How to reach you'), '0917 555 0101')
  await user.type(within(f).getByLabelText('Event date'), '2030-04-20')
  await user.type(within(f).getByLabelText('Venue'), 'Limketkai Atrium')
}

describe('the booking form', () => {
  beforeEach(() => {
    // 2030-03-09 17:30 UTC is already 10 March in Manila: "today" is the 10th.
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2030-03-09T17:30:00Z'))
  })
  afterEach(() => vi.useRealTimers())

  it("uses Manila's date for today, and allows up to two years ahead", () => {
    expect(businessToday()).toBe('2030-03-10')
    expect(latestEventDate('2030-03-10')).toBe('2032-03-10')
    expect(latestEventDate('2028-02-29')).toBe('2030-02-28')

    const values = { ...EMPTY_BOOKING, name: 'K', contact: 'c', venue: 'v' }
    expect(validateBooking({ ...values, eventDate: '2030-03-09' }, '2030-03-10').eventDate).toMatch(/passed/)
    expect(validateBooking({ ...values, eventDate: '2030-03-10' }, '2030-03-10')).toEqual({})
    expect(validateBooking({ ...values, eventDate: '2032-03-11' }, '2030-03-10').eventDate).toMatch(/2 years/)
    expect(validateBooking({ ...values, eventDate: '2030-03-10', guestCount: '0' }, '2030-03-10').guestCount).toBeDefined()
    expect(validateBooking({ ...values, eventDate: '2030-03-10', guestCount: '12.5' }, '2030-03-10').guestCount).toBeDefined()
    expect(validateBooking({ ...values, eventDate: '2030-03-10', guestCount: '5000' }, '2030-03-10')).toEqual({})
  })

  it('shows what is missing under each field on submit, with focus on the first, and sends nothing', async () => {
    let posts = 0
    server.use(http.post(BOOKINGS_URL, () => {
      posts += 1
      return HttpResponse.json({ received: true }, { status: 201 })
    }))
    const user = userEvent.setup()
    render(<Book home={home} />)
    const f = await form()

    expect(within(f).getByLabelText('Event date')).toHaveAttribute('min', '2030-03-10')
    await user.click(within(f).getByRole('button', { name: 'Send booking request' }))

    expect(within(f).getByLabelText('Your name')).toHaveFocus()
    expect(within(f).getByLabelText('Your name')).toHaveAttribute('aria-invalid', 'true')
    expect(within(f).getByLabelText('Your name')).toHaveAccessibleDescription('Please tell us your name.')
    expect(within(f).getByLabelText('How to reach you')).toHaveAccessibleDescription(/Mobile number, Messenger or Instagram Please tell us how to reach you\./)
    expect(within(f).getByLabelText('Event date')).toHaveAccessibleDescription('Please pick the event date.')
    expect(within(f).getByLabelText('Venue')).toHaveAccessibleDescription('Please tell us where the event is.')
    expect(within(f).getByLabelText('Number of guests (optional)')).not.toHaveAttribute('aria-invalid')
    expect(posts).toBe(0)
  })

  it("sends the request, confirms with focus, and \"Send another\" brings the form back empty", async () => {
    let body: Record<string, unknown> | null = null
    server.use(http.post(BOOKINGS_URL, async ({ request }) => {
      body = (await request.json()) as Record<string, unknown>
      return HttpResponse.json({ received: true }, { status: 201 })
    }))
    const user = userEvent.setup()
    render(<Book home={home} />)
    const f = await form()

    await fillValid(user, f)
    await user.type(within(f).getByLabelText('Number of guests (optional)'), '60')
    await user.selectOptions(within(f).getByLabelText('Package'), 'Regular')
    await user.type(within(f).getByLabelText('Anything else (optional)'), 'Afternoon.')
    expect(within(f).getByText('10 / 1000')).toBeInTheDocument()
    await user.click(within(f).getByRole('button', { name: 'Send booking request' }))

    const done = await screen.findByText('Request sent. Klowee will get back to you.')
    await waitFor(() => expect(done.parentElement).toHaveFocus())
    expect(body).toEqual({
      name: 'Kee Santos', contact: '0917 555 0101', eventDate: '2030-04-20', venue: 'Limketkai Atrium',
      guestCount: 60, packageId: 'p-2', message: 'Afternoon.', website: '',
    })

    await user.click(screen.getByRole('button', { name: 'Send another' }))
    const again = await form()
    expect(within(again).getByLabelText('Your name')).toHaveValue('')
    expect(within(again).getByLabelText('Package')).toHaveValue('')
  })

  it("shows the API's own field errors the same way", async () => {
    server.use(http.post(BOOKINGS_URL, () =>
      HttpResponse.json({ status: 400, errors: { eventDate: ['That date has passed. Please pick today or later.'], '$.guestCount': ['Not a number.'] } }, { status: 400 })))
    const user = userEvent.setup()
    render(<Book home={home} />)
    const f = await form()

    await fillValid(user, f)
    await user.click(within(f).getByRole('button', { name: 'Send booking request' }))

    const date = within(f).getByLabelText('Event date')
    await waitFor(() => expect(date).toHaveAccessibleDescription('That date has passed. Please pick today or later.'))
    expect(date).toHaveFocus()
    expect(within(f).getByLabelText('Number of guests (optional)')).toHaveAccessibleDescription('Not a number.')
    expect(within(f).getByLabelText('Your name')).toHaveValue('Kee Santos')
  })

  it.each([
    ['too many requests', () => HttpResponse.json({ status: 429, detail: "You've sent a few requests already." }, { status: 429 }), "You've sent a few requests already."],
    ['the form switched off meanwhile', () => HttpResponse.json({ status: 409, detail: 'Booking requests are switched off right now.' }, { status: 409 }), 'Booking requests are switched off right now.'],
    ['no network', () => HttpResponse.error(), /Couldn't reach Klowee/],
  ])('on %s, shows one message above the button and keeps what was typed', async (_, reply, text) => {
    server.use(http.post(BOOKINGS_URL, reply))
    const user = userEvent.setup()
    render(<Book home={home} />)
    const f = await form()

    await fillValid(user, f)
    await user.click(within(f).getByRole('button', { name: 'Send booking request' }))

    expect(await within(f).findByRole('alert')).toHaveTextContent(text)
    expect(within(f).getByLabelText('Your name')).toHaveValue('Kee Santos')
    expect(within(f).getByLabelText('Venue')).toHaveValue('Limketkai Atrium')
    expect(within(f).getByRole('button', { name: 'Send booking request' })).toBeEnabled()
  })

  it('says it is sending, and the button is disabled meanwhile', async () => {
    let release: () => void = () => {}
    server.use(http.post(BOOKINGS_URL, () => new Promise<Response>((resolve) => {
      release = () => resolve(HttpResponse.json({ received: true }, { status: 201 }))
    })))
    const user = userEvent.setup()
    render(<Book home={home} />)
    const f = await form()
    await fillValid(user, f)

    await user.click(within(f).getByRole('button', { name: 'Send booking request' }))
    expect(await within(f).findByRole('button', { name: 'Sending your request…' })).toBeDisabled()
    release()
    expect(await screen.findByText('Request sent. Klowee will get back to you.')).toBeInTheDocument()
  })

  it('keeps the honeypot out of sight, out of the tab order and away from screen readers', async () => {
    render(<Book home={home} />)
    const f = await form()

    const trap = f.querySelector('input[name="website"]')!
    expect(trap).toHaveAttribute('tabindex', '-1')
    expect(trap).toHaveAttribute('autocomplete', 'off')
    expect(trap.closest('[aria-hidden="true"]')).not.toBeNull()
    expect(within(f).queryByRole('textbox', { name: 'Website' })).not.toBeInTheDocument()
  })

  it('lists the active packages plus "Not sure yet", and tells visitors how their details are used', async () => {
    render(<Book home={home} />)
    const f = await form()

    expect(within(within(f).getByLabelText('Package')).getAllByRole('option').map((o) => o.textContent)).toEqual(['Not sure yet', 'Starter', 'Regular'])
    expect(within(f).getByText('Your details are only used to reply about this booking.')).toBeInTheDocument()
    expect(screen.getByText('or message us')).toBeInTheDocument()
  })

  it('a package card preselects that package, a second card overrides it', async () => {
    vi.useRealTimers()
    serveHome(home)
    const user = userEvent.setup()
    render(<App />)

    await user.click(await screen.findByRole('link', { name: 'Book this: Regular' }))
    const f = await form()
    expect(within(f).getByLabelText('Package')).toHaveValue('p-2')

    await user.click(screen.getByRole('link', { name: 'Book this: Starter' }))
    expect(within(f).getByLabelText('Package')).toHaveValue('p-1')
    expect(screen.getByRole('link', { name: 'Book this: Starter' })).toHaveAttribute('href', '#booking-form')
  })
})

describe('the Book section with the form switched off', () => {
  it('has no form and looks as it did: the line and the contact buttons only', () => {
    const off = { ...home, settings: { ...home.settings, booking_form_enabled: 'false', booking_form_note: 'Book two weeks ahead.' } }
    const { container } = render(<Book home={off} />)

    expect(screen.queryByRole('form')).not.toBeInTheDocument()
    expect(screen.queryByText('or message us')).not.toBeInTheDocument()
    expect(screen.queryByText('Book two weeks ahead.')).not.toBeInTheDocument()
    expect(container.querySelector('#booking-form')).toBeNull()
    expect(screen.getByRole('link', { name: 'Email us' })).toBeInTheDocument()
    expect(screen.getByText('Tell us the date and headcount.')).toBeInTheDocument()
  })

  it('sends the package cards to the Book section, as before', async () => {
    serveHome({ ...home, settings: { ...home.settings, booking_form_enabled: 'FALSE' } })
    render(<App />)

    expect(await screen.findByRole('link', { name: 'Book this: Starter' })).toHaveAttribute('href', '#book')
    expect(screen.queryByRole('form', { name: 'Booking request' })).not.toBeInTheDocument()
  })

  it("shows the owners' note above the form when it's on", async () => {
    render(<Book home={{ ...home, settings: { ...home.settings, booking_form_note: 'Book two weeks ahead.' } }} />)
    expect(screen.getByText('Book two weeks ahead.')).toBeInTheDocument()
    await form()
  })
})
