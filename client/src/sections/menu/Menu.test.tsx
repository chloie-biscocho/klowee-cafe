import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { emptyHome, fullHome } from '../../test/fixtures'
import { Menu } from './Menu'

describe('Menu', () => {
  it('prints categories in ovals with their description, skipping empty ones', () => {
    render(<Menu home={fullHome} />)

    expect(screen.getByRole('heading', { name: 'klowee cafe pop-up menu' })).toBeInTheDocument()
    expect(screen.getAllByRole('heading', { level: 3 }).map((h) => h.textContent)).toEqual(['Coffee', 'Houseblend Matcha', 'Okumidori Matcha'])
    expect(screen.getByText('oat milk + ceremonial matcha')).toBeInTheDocument()
    expect(screen.queryByText('Seasonal')).not.toBeInTheDocument()
  })

  it('opens a drink with a description; a drink without one is a plain row', async () => {
    const user = userEvent.setup()
    const { container } = render(<Menu home={fullHome} />)

    const details = container.querySelectorAll('details')
    expect(details).toHaveLength(1)
    const latte = details[0]!
    expect(latte).not.toHaveAttribute('open')
    await user.click(within(latte).getByText('Spanish Latte'))
    expect(latte).toHaveAttribute('open')
    expect(within(latte).getByText('Espresso, milk, and condensed milk.')).toBeVisible()

    const americano = screen.getByText('Americano').closest('div')!
    expect(americano.closest('details')).toBeNull()
    expect(americano).toHaveTextContent('110')
  })

  it('marks a featured drink "best seller" and drops the blend its category names', () => {
    render(<Menu home={fullHome} />)

    expect(screen.getByText('best seller').closest('summary')).toHaveTextContent('Spanish Latte')
    expect(screen.getAllByText('Banana Matcha')).toHaveLength(2)
    expect(screen.getByText('99.50')).toBeInTheDocument()
  })

  it('says "menu coming soon" on the same paper when no menu is published', () => {
    render(<Menu home={emptyHome} />)

    expect(screen.getByRole('heading', { name: 'klowee cafe pop-up menu' })).toBeInTheDocument()
    expect(screen.getByText('menu coming soon')).toBeInTheDocument()
  })
})
