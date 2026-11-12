import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { emptyHome, fullHome } from '../test/fixtures'
import { Book } from './Book'
import { Packages } from './Packages'
import { Story } from './Story'

describe('Story, Packages, Book', () => {
  it('splits the story into paragraphs, and hides when heading and body are both empty', () => {
    const { container, rerender } = render(<Story home={fullHome} />)
    expect(screen.getByRole('heading', { name: 'Our story' })).toBeInTheDocument()
    expect(container.querySelectorAll('p')).toHaveLength(2)

    rerender(<Story home={emptyHome} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('shows one card per package with its inclusions, and nothing without packages', () => {
    const { container, rerender } = render(<Packages home={fullHome} />)
    expect(screen.getByRole('heading', { name: 'Starter' })).toBeInTheDocument()
    expect(screen.getByText('₱7,500')).toBeInTheDocument()
    expect(screen.getByText('2 baristas')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Book this: Starter' })).toHaveAttribute('href', '#book')

    rerender(<Packages home={emptyHome} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('offers only the contact buttons whose settings are filled in', () => {
    render(<Book home={fullHome} />)

    expect(screen.getByRole('link', { name: 'Email us' })).toHaveAttribute('href', 'mailto:hello@kloweecafe.com')
    expect(screen.getByRole('link', { name: 'Message on Instagram' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Facebook' })).not.toBeInTheDocument()
  })
})
