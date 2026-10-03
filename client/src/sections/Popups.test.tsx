import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { emptyHome, fullHome } from '../test/fixtures'
import { Popups } from './Popups'

describe('Popups', () => {
  it('puts upcoming events first with a "next" sticker, and says where we are headed', () => {
    render(<Popups home={fullHome} />)

    expect(screen.getByRole('heading', { name: "where we're headed" })).toBeInTheDocument()
    const polaroids = screen.getAllByRole('button')
    expect(polaroids[0]).toHaveTextContent('Uptown Night Market')
    expect(within(polaroids[0]!).getByText('next')).toBeInTheDocument()
    expect(within(polaroids[1]!).queryByText('next')).not.toBeInTheDocument()
  })

  it('expands a polaroid on click, keeps one open at a time, and closes on Escape', async () => {
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

    await user.keyboard('{Escape}')
    expect(screen.queryByRole('region', { name: 'Uptown Night Market' })).not.toBeInTheDocument()
    expect(next).toHaveAttribute('aria-expanded', 'false')
    // Focus goes back to the polaroid that opened it.
    expect(next).toHaveFocus()
  })

  it('opens and closes from the keyboard alone', async () => {
    const user = userEvent.setup()
    render(<Popups home={fullHome} />)

    await user.tab()
    expect(screen.getByRole('button', { name: /Uptown Night Market/ })).toHaveFocus()
    await user.keyboard('{Enter}')
    expect(screen.getByRole('region', { name: 'Uptown Night Market' })).toBeInTheDocument()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('region', { name: 'Uptown Night Market' })).not.toBeInTheDocument()
  })

  it('shows the tent sketch empty state when no events are published', () => {
    render(<Popups home={emptyHome} />)

    expect(screen.getByRole('heading', { name: "where we've been" })).toBeInTheDocument()
    expect(screen.getByText('no pop-ups logged yet')).toBeInTheDocument()
  })
})
