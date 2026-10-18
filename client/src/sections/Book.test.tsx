import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { emptyHome, fullHome } from '../test/fixtures'
import { Book } from './Book'

describe('Book', () => {
  it('shows the book_body line under the heading', () => {
    render(<Book home={fullHome} />)

    expect(screen.getByText('Tell us the date and headcount.')).toBeInTheDocument()
  })

  it('leaves the line out when the setting is blank', () => {
    const { container } = render(<Book home={emptyHome} />)

    expect(container.querySelector('p')).toBeNull()
  })
})
