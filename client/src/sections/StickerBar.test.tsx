import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { emptyHome, fullHome } from '../test/fixtures'
import { StickerBar } from './StickerBar'

describe('StickerBar', () => {
  it('announces the next pop-up and links to the pop-ups section', () => {
    render(<StickerBar home={fullHome} />)

    const link = screen.getByRole('link', { name: 'next pop-up: Uptown Night Market · May 21 to 24, 2099' })
    expect(link).toHaveAttribute('href', '#popups')
  })

  it('falls back to the ticker text, linking to booking, when no event is next', () => {
    render(<StickerBar home={{ ...fullHome, nextEvent: null }} />)

    expect(screen.getByRole('link', { name: 'Now booking pop-ups around Cagayan de Oro' })).toHaveAttribute(
      'href',
      '#book',
    )
  })

  it('renders nothing with no event and no fallback text', () => {
    const { container } = render(
      <StickerBar home={{ ...emptyHome, settings: { ticker_fallback: '   ' } }} />,
    )

    expect(container).toBeEmptyDOMElement()
  })

  it('shows the announcement instead of the next event, with its link', () => {
    render(
      <StickerBar
        home={{ ...fullHome, announcement: { text: 'Free upsize this week', linkUrl: 'https://instagram.com/p/1' } }}
      />,
    )

    expect(screen.getByRole('link', { name: 'Free upsize this week' })).toHaveAttribute(
      'href',
      'https://instagram.com/p/1',
    )
    expect(screen.queryByText(/next pop-up/)).not.toBeInTheDocument()
  })

  it('shows an announcement without a link as plain text', () => {
    render(<StickerBar home={{ ...fullHome, announcement: { text: 'Closed Monday', linkUrl: null } }} />)

    expect(screen.getByText('Closed Monday')).toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })

  it('holds its place, empty and hidden from screen readers, while the page loads', () => {
    const { container } = render(<StickerBar home={null} />)

    const strip = container.firstElementChild!
    expect(strip).toHaveAttribute('aria-hidden', 'true')
    expect(strip).toHaveTextContent('')
    expect(strip.firstElementChild!.className).toContain('min-h-')
  })
})
