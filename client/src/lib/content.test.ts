import { describe, expect, it } from 'vitest'
import { event, fullHome } from '../test/fixtures'
import type { PublicMenuCategory } from '../api/types'
import { displayName, menuColumns, polaroidBadge, shelfDrinks } from './content'
import { CUP_LAYERS, DEFAULT_LAYERS, cupLayersFor } from './cupLayers'

const category = (id: string, count: number): PublicMenuCategory => ({
  id,
  name: id,
  items: Array.from({ length: count }, (_, i) => ({ id: `${id}${i}`, name: `${id}${i}`, description: '', price: 1, photoUrl: null, isFeatured: false })),
})

describe('content rules', () => {
  it('puts each drink on the shelf once: the two matcha blends are one cup', () => {
    expect(shelfDrinks(fullHome.menu)).toEqual(['Americano', 'Spanish Latte', 'Banana Matcha', 'Mystery Fizz'])
    expect(shelfDrinks(null)).toEqual([])
  })

  it('gives a known drink its layers and an unknown drink the default ones', () => {
    expect(Object.keys(CUP_LAYERS)).toHaveLength(25)
    expect(cupLayersFor('Banana Matcha (Okumidori)')).toBe(CUP_LAYERS['banana matcha'])
    expect(cupLayersFor('  Spanish   LATTE ')).toBe(CUP_LAYERS['spanish latte'])
    expect(cupLayersFor('Mystery Fizz')).toBe(DEFAULT_LAYERS)
  })

  it('splits the menu into two columns in order, balanced by item count', () => {
    const [left, right] = menuColumns([category('a', 8), category('b', 5), category('c', 4), category('d', 6), category('e', 6)])
    expect(left.map((c) => c.id)).toEqual(['a', 'b'])
    expect(right.map((c) => c.id)).toEqual(['c', 'd', 'e'])
    expect(menuColumns([])).toEqual([[], []])
  })

  it('drops a blend note only when the category already says it', () => {
    expect(displayName('Banana Matcha (Houseblend)', 'Houseblend Matcha')).toBe('Banana Matcha')
    expect(displayName('Iced Latte (Large)', 'Coffee')).toBe('Iced Latte (Large)')
  })

  it('stickers "next" on the upcoming event, else "latest" on the newest', () => {
    const events = [event({ id: 'up', name: 'Up', status: 'Upcoming' }), event({ id: 'past', name: 'Past' })]
    expect(polaroidBadge(events, 'up')).toEqual({ id: 'up', label: 'next' })
    expect(polaroidBadge(events.slice(1), null)).toEqual({ id: 'past', label: 'latest' })
    expect(polaroidBadge([], null)).toBeNull()
  })
})
