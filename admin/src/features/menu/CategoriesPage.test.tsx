import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, menuItems, signedIn } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { server } from '../../test/server'
import type { MenuCategoryDto, MenuItemDto } from '../../types/api'
import { archivedOnly } from './categoryGroups'

const coffee: MenuCategoryDto = { id: 'cat-coffee', name: 'Coffee', sortOrder: 1 }
const fizz: MenuCategoryDto = { id: 'cat-fizz', name: 'Fruit soda', sortOrder: 4 }
const empty: MenuCategoryDto = { id: 'cat-empty', name: 'Seasonal', sortOrder: 5 }
const item = (name: string, categoryId: string, isArchived: boolean): MenuItemDto => ({
  ...menuItems[0]!, id: name, name, categoryId, isArchived,
})
const items = [item('Americano', 'cat-coffee', false), item('Old Latte', 'cat-coffee', true), item('Blueberry Fizz', 'cat-fizz', true)]

describe('Categories', () => {
  it('finds categories whose items are all archived', () => {
    expect([...archivedOnly(items)]).toEqual(['cat-fizz'])
    expect(archivedOnly([])).toEqual(new Set())
  })

  it('marks them "(archived items only)", muted, and nothing else', async () => {
    server.use(
      http.get(`${API}/menu/categories`, () => HttpResponse.json([coffee, fizz, empty])),
      http.get(`${API}/menu/items`, () => HttpResponse.json(items)),
    )
    renderApp('/menu/categories', signedIn)

    const fizzRow = (await screen.findByText('Fruit soda')).closest('tr')!
    expect(await within(fizzRow).findByText('(archived items only)')).toHaveClass('text-muted')
    expect(within(screen.getByText('Coffee').closest('tr')!).queryByText('(archived items only)')).not.toBeInTheDocument()
    expect(within(screen.getByText('Seasonal').closest('tr')!).queryByText('(archived items only)')).not.toBeInTheDocument()
  })
})
