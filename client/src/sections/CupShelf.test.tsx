import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { fullHome } from '../test/fixtures'
import { DEFAULT_LAYERS } from '../lib/cupLayers'
import { CupShelf } from './CupShelf'

describe('CupShelf', () => {
  it('shows one labelled cup per drink, matcha blends once', () => {
    render(<CupShelf menu={fullHome.menu} />)

    const cups = screen.getAllByRole('img')
    expect(cups.map((cup) => cup.getAttribute('aria-label'))).toEqual(['Americano', 'Spanish Latte', 'Banana Matcha', 'Mystery Fizz'])
    expect(cups[2]).toHaveAttribute('title', 'Banana Matcha')
  })

  it('draws a drink it has no colours for with the default layers', () => {
    render(<CupShelf menu={fullHome.menu} />)

    const glass = screen.getByRole('img', { name: 'Mystery Fizz' }).firstElementChild as HTMLElement
    expect(glass.style.getPropertyValue('--a')).toBe(DEFAULT_LAYERS.a)
    expect(glass.style.getPropertyValue('--s2')).toBe(DEFAULT_LAYERS.s2)
  })

  it('has real arrow buttons, the earlier one disabled at the start', () => {
    render(<CupShelf menu={fullHome.menu} />)

    expect(screen.getByRole('button', { name: 'Show earlier drinks' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Show more drinks' })).toBeInTheDocument()
  })

  it('is not rendered without a menu', () => {
    const { container } = render(<CupShelf menu={null} />)

    expect(container).toBeEmptyDOMElement()
  })
})
