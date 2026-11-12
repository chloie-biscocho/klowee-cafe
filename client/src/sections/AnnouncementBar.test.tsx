import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { emptyHome, fullHome } from '../test/fixtures'
import { AnnouncementBar } from './AnnouncementBar'

describe('AnnouncementBar', () => {
  it('shows the announcement rather than the next pop-up', () => {
    render(<AnnouncementBar home={{ ...fullHome, announcement: { text: 'Free upsize this week', linkUrl: 'https://instagram.com/p/1' } }} />)

    expect(screen.getByRole('link', { name: 'Free upsize this week' })).toHaveAttribute('href', 'https://instagram.com/p/1')
    expect(screen.queryByText(/next pop-up/)).not.toBeInTheDocument()
  })

  it('falls back to the next pop-up, linking to the pop-ups section', () => {
    render(<AnnouncementBar home={fullHome} />)

    expect(screen.getByRole('link', { name: 'next pop-up: Uptown Night Market, May 21 to 24, 2099' })).toHaveAttribute('href', '#popups')
  })

  it('renders nothing with neither, even when the old ticker fallback is set', () => {
    const { container } = render(<AnnouncementBar home={{ ...emptyHome, settings: { ticker_fallback: 'Now booking' } }} />)

    expect(container).toBeEmptyDOMElement()
  })
})
