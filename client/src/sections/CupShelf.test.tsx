import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { fullHome } from '../test/fixtures'
import { DEFAULT_LAYERS } from '../lib/cupLayers'
import { shelfWindow } from '../lib/shelf'
import { CupShelf, TAP_LIFT_MS } from './CupShelf'

describe('CupShelf', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    vi.useRealTimers()
  })

  it('shows one labelled cup per drink, matcha blends once', () => {
    render(<CupShelf menu={fullHome.menu} />)

    const cups = screen.getAllByRole('img')
    expect(cups.map((cup) => cup.getAttribute('aria-label'))).toEqual(['Americano', 'Spanish Latte', 'Banana Matcha', 'Mystery Fizz'])
    // The name a hover shows: visible text, hidden from screen readers (the label already says it).
    expect(cups[2]!.querySelector('.cup-name')).toHaveTextContent('Banana Matcha')
    expect(cups[2]!.querySelector('.cup-name')).toHaveAttribute('aria-hidden', 'true')
  })

  it('draws a drink it has no colours for with the default layers', () => {
    render(<CupShelf menu={fullHome.menu} />)

    const glass = screen.getByRole('img', { name: 'Mystery Fizz' }).querySelector('.cup-glass') as HTMLElement
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

  it('stands cups straight at rest, and tilts only the hovered one, alternating by position', () => {
    render(<CupShelf menu={fullHome.menu} />)
    const cups = screen.getAllByRole('img')
    const tilted = () => cups.filter((cup) => cup.classList.contains('cup-tilt-left') || cup.classList.contains('cup-tilt-right'))

    expect(tilted()).toEqual([])
    expect(cups.some((cup) => cup.hasAttribute('data-lifted'))).toBe(false)

    fireEvent.pointerEnter(cups[1]!, { pointerType: 'mouse' })
    expect(cups[1]).toHaveAttribute('data-lifted')
    expect(cups[1]).toHaveClass('cup-tilt-right')
    expect(tilted()).toEqual([cups[1]])
    fireEvent.pointerLeave(cups[1]!, { pointerType: 'mouse' })
    expect(cups[1]).not.toHaveAttribute('data-lifted')
    expect(tilted()).toEqual([])

    // Its neighbour leans the other way.
    fireEvent.pointerEnter(cups[2]!, { pointerType: 'mouse' })
    expect(cups[2]).toHaveClass('cup-tilt-left')
    fireEvent.pointerLeave(cups[2]!, { pointerType: 'mouse' })
  })

  it('lifts a tapped cup briefly on touch, without getting stuck', () => {
    vi.useFakeTimers()
    render(<CupShelf menu={fullHome.menu} />)
    const cup = screen.getByRole('img', { name: 'Spanish Latte' })

    // A touch also sends enter and leave; only the tap counts.
    fireEvent.pointerEnter(cup, { pointerType: 'touch' })
    expect(cup).not.toHaveAttribute('data-lifted')
    fireEvent.pointerDown(cup, { pointerType: 'touch' })
    fireEvent.pointerLeave(cup, { pointerType: 'touch' })
    expect(cup).toHaveAttribute('data-lifted')

    act(() => vi.advanceTimersByTime(TAP_LIFT_MS))
    expect(cup).not.toHaveAttribute('data-lifted')
  })

  it('lets go at once when a touch on a cup turns into a swipe or a scroll', () => {
    render(<CupShelf menu={fullHome.menu} />)
    const cup = screen.getByRole('img', { name: 'Americano' })

    fireEvent.pointerDown(cup, { pointerType: 'touch' })
    expect(cup).toHaveAttribute('data-lifted')
    // The browser cancels the pointer when it takes the gesture over for scrolling.
    fireEvent.pointerCancel(cup, { pointerType: 'touch' })
    expect(cup).not.toHaveAttribute('data-lifted')
  })

  it('shows the lifted state on every cup when the shelf has keyboard focus (CSS), without adding tab stops', () => {
    render(<CupShelf menu={fullHome.menu} />)

    const shelf = screen.getByRole('group', { name: 'All drinks, shown as layered cups' })
    expect(shelf).toHaveClass('cup-shelf')
    expect(shelf).toHaveAttribute('tabindex', '0')
    for (const cup of screen.getAllByRole('img')) expect(cup).not.toHaveAttribute('tabindex')
  })

  it('pages exactly one window of whole cups per arrow press', () => {
    // jsdom has no layout: give the row, the arrows and the cups their sizes on a 390px phone.
    const sizes = (el: Element, name: 'offsetWidth' | 'clientWidth' | 'scrollWidth') => {
      if (name === 'offsetWidth') return el.classList.contains('shelf-cup') ? 58 : el.tagName === 'BUTTON' ? 36 : 0
      if (el.getAttribute('role') === 'group') return name === 'scrollWidth' ? 4 * 71 : 150
      return el.firstElementChild?.tagName === 'BUTTON' ? 346 : 0
    }
    for (const name of ['offsetWidth', 'clientWidth', 'scrollWidth'] as const)
      vi.spyOn(HTMLElement.prototype, name, 'get').mockImplementation(function (this: HTMLElement) {
        return sizes(this, name)
      })
    const scrollBy = vi.fn()
    HTMLElement.prototype.scrollBy = scrollBy

    render(<CupShelf menu={fullHome.menu} />)
    fireEvent.click(screen.getByRole('button', { name: 'Show more drinks' }))

    // No CSS in jsdom, so gaps and padding read 0: 346 - 2 × 36 = 274 of room fits 4 cups of 58.
    const expected = shelfWindow({ row: 346, arrow: 36, rowGap: 0, cup: 58, gap: 0, pad: 0, drinks: 4 })!
    expect(scrollBy).toHaveBeenCalledWith({ left: expected.page, behavior: 'smooth' })
    expect(expected.page).toBe(4 * 58)
  })
})

describe('shelfWindow', () => {
  // The shelf's real sizes: phone cups 58px with 13px gaps and 36px arrows, 8px apart;
  // from 760px, 74px cups with 24px gaps and 44px arrows, 14px apart. 12px padding each side.
  const phone = { arrow: 36, rowGap: 8, cup: 58, gap: 13, pad: 12, drinks: 30 }
  const computer = { arrow: 44, rowGap: 14, cup: 74, gap: 24, pad: 12, drinks: 30 }

  it.each([
    ['320px phone', 320 - 44, phone, 2],
    ['390px phone', 390 - 44, phone, 3],
    ['430px phone', 430 - 44, phone, 4],
    ['768px tablet', 768 - 44, computer, 6],
    ['1280px computer', 1000 - 44, computer, 8],
  ])('fits whole cups only at %s', (_, row, sizes, count) => {
    const fitted = shelfWindow({ row, ...sizes })!
    const room = row - 2 * (sizes.arrow + sizes.rowGap)

    expect(fitted.count).toBe(count)
    // The window holds exactly `count` cups and their padding, and fits between the arrows…
    expect(fitted.width).toBe(count * sizes.cup + (count - 1) * sizes.gap + 2 * sizes.pad)
    expect(fitted.width).toBeLessThanOrEqual(room)
    // …and one more cup would not have fitted (unless held at the two-cup minimum).
    if (count > 2) expect(fitted.width + sizes.cup + sizes.gap).toBeGreaterThan(room)
    // An arrow pages exactly one window.
    expect(fitted.page).toBe(count * (sizes.cup + sizes.gap))
  })

  it('never shows more cups than there are drinks', () => {
    expect(shelfWindow({ row: 956, ...computer, drinks: 3 })!.count).toBe(3)
  })
})
