import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { emptyHome, fullHome } from '../test/fixtures'
import { Menu } from './Menu'

describe('Menu', () => {
  it('renders one card per category, skips empty ones, and stickers the featured item', () => {
    render(<Menu home={fullHome} />)

    const cards = screen.getAllByRole('article')
    expect(cards.map((card) => within(card).getByRole('heading').textContent)).toEqual(['Coffee', 'Matcha'])
    expect(screen.queryByText('Seasonal')).not.toBeInTheDocument()

    const coffee = within(cards[0]!)
    expect(coffee.getByText('₱110')).toBeInTheDocument()
    const ube = coffee.getByText('Ube Latte').closest('li')!
    expect(within(ube).getByText('best seller')).toBeInTheDocument()
    const americano = coffee.getByText('Americano').closest('li')!
    expect(within(americano).queryByText('best seller')).not.toBeInTheDocument()

    expect(screen.getByText('April 2026 pop-up')).toBeInTheDocument()
  })

  it('shows "menu coming soon" when there is no live menu', () => {
    render(<Menu home={emptyHome} />)

    expect(screen.getByRole('heading', { name: "what's in the cart" })).toBeInTheDocument()
    expect(screen.getByText('menu coming soon')).toBeInTheDocument()
    expect(screen.queryByRole('article')).not.toBeInTheDocument()
  })
})
