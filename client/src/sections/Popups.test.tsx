import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { emptyHome, event, fullHome } from '../test/fixtures'
import { Popups } from './Popups'

describe('Popups', () => {
  it('puts the upcoming event first with a "next" sticker, and says where we are headed', () => {
    render(<Popups home={fullHome} />)

    expect(screen.getByRole('heading', { name: "Where we're headed" })).toBeInTheDocument()
    const polaroids = screen.getAllByRole('button')
    expect(polaroids[0]).toHaveTextContent('Uptown Night Market')
    expect(within(polaroids[0]!).getByText('next')).toBeInTheDocument()
    expect(within(polaroids[0]!).getByText('1 photo')).toBeInTheDocument()
    expect(within(polaroids[1]!).queryByText('next')).not.toBeInTheDocument()
    expect(screen.queryByText('latest')).not.toBeInTheDocument()
  })

  it('with nothing upcoming, puts "latest" on the newest and says where we have been', () => {
    const events = [event({ id: 'new', name: 'Scout Market', startsOn: '2026-07-22', endsOn: '2026-07-26' }), event({ id: 'old', name: 'First pop-up' })]
    render(<Popups home={{ ...fullHome, nextEvent: null, events }} />)

    expect(screen.getByRole('heading', { name: "Where we've been" })).toBeInTheDocument()
    const [newest, older] = screen.getAllByRole('button')
    expect(within(newest!).getByText('latest')).toBeInTheDocument()
    expect(within(older!).queryByText('latest')).not.toBeInTheDocument()
    expect(screen.queryByText('next')).not.toBeInTheDocument()
  })

  it('opens one panel at a time; Close and Escape close it and return focus', async () => {
    const user = userEvent.setup()
    render(<Popups home={fullHome} />)

    const past = screen.getByRole('button', { name: /First pop-up/ })
    const next = screen.getByRole('button', { name: /Uptown Night Market/ })

    await user.click(past)
    expect(past).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('region', { name: 'First pop-up' })).toHaveTextContent('Where it all started.')

    await user.click(next)
    expect(past).toHaveAttribute('aria-expanded', 'false')
    const panel = screen.getByRole('region', { name: 'Uptown Night Market' })
    expect(within(panel).getByRole('img', { name: 'Setting up' })).toBeInTheDocument()
    expect(screen.queryByRole('region', { name: 'First pop-up' })).not.toBeInTheDocument()

    await user.click(within(panel).getByRole('button', { name: 'Close' }))
    expect(screen.queryByRole('region', { name: 'Uptown Night Market' })).not.toBeInTheDocument()
    expect(next).toHaveFocus()

    await user.click(past)
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('region', { name: 'First pop-up' })).not.toBeInTheDocument()
    expect(past).toHaveAttribute('aria-expanded', 'false')
    expect(past).toHaveFocus()
  })

  it('shows the tent sketch empty state when no events are published', () => {
    render(<Popups home={emptyHome} />)

    expect(screen.getByRole('heading', { name: "Where we've been" })).toBeInTheDocument()
    expect(screen.getByText('no pop-ups logged yet')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Book the cart' })).toHaveAttribute('href', '#book')
  })
})
