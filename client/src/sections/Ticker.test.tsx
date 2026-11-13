import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { emptyHome, fullHome } from '../test/fixtures'
import { Ticker } from './Ticker'

const announcement = { text: 'Free upsize this week', linkUrl: 'https://instagram.com/p/1' }

describe('Ticker', () => {
  it('shows the announcement first, linked when it has a link', () => {
    render(<Ticker home={{ ...fullHome, announcement }} />)

    expect(screen.getByRole('link', { name: 'Free upsize this week' })).toHaveAttribute('href', 'https://instagram.com/p/1')
    expect(screen.queryByText(/Next pop-up/)).not.toBeInTheDocument()
    expect(screen.queryByText('Now booking pop-ups around Cagayan de Oro')).not.toBeInTheDocument()
  })

  it('shows an announcement without a link as plain text', () => {
    render(<Ticker home={{ ...fullHome, announcement: { text: 'Closed Monday', linkUrl: null } }} />)

    expect(screen.getByText('Closed Monday')).toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })

  it('without an announcement, names the next pop-up with dates and venue, and links to it', () => {
    const { container } = render(<Ticker home={fullHome} />)

    expect(container).toHaveTextContent('Next pop-up: Uptown Night Market, May 21 to 24, 2099, Uptown Mall. See details')
    expect(screen.getByRole('link', { name: 'See details' })).toHaveAttribute('href', '#popups')
    expect(screen.queryByText('Now booking pop-ups around Cagayan de Oro')).not.toBeInTheDocument()
  })

  it('with neither, shows the fallback line, the whole line linking to booking', () => {
    render(<Ticker home={{ ...fullHome, nextEvent: null }} />)

    expect(screen.getByRole('link', { name: 'Now booking pop-ups around Cagayan de Oro' })).toHaveAttribute('href', '#book')
  })

  it('renders nothing when all three are empty', () => {
    const { container } = render(<Ticker home={{ ...emptyHome, settings: { ticker_fallback: '   ' } }} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('hides the pulsing dot from assistive tech', () => {
    const { container } = render(<Ticker home={fullHome} />)

    expect(container.querySelector('.ticker-dot')).toHaveAttribute('aria-hidden', 'true')
  })

  it('holds one empty line of space, hidden from screen readers, while the page loads', () => {
    const { container } = render(<Ticker home={null} />)

    expect(container.firstElementChild).toHaveAttribute('aria-hidden', 'true')
    expect(container.firstElementChild?.textContent?.trim()).toBe('')
  })
})
