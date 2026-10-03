import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { emptyHome, fullHome } from '../test/fixtures'
import { Packages } from './Packages'

describe('Packages', () => {
  it('is absent entirely when there are no packages', () => {
    const { container } = render(<Packages home={emptyHome} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders each package with price, note, inclusions and a booking link', () => {
    render(<Packages home={fullHome} />)

    expect(screen.getByRole('heading', { name: 'Starter' })).toBeInTheDocument()
    expect(screen.getByText('₱7,500')).toBeInTheDocument()
    expect(screen.getByText('good for 50 pax')).toBeInTheDocument()
    expect(screen.getByText('2 baristas')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Book this: Starter' })).toHaveAttribute('href', '#book')
  })
})
