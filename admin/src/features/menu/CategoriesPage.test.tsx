import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, menuItems, signedIn } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { server } from '../../test/server'
import type { MenuCategoryDto, MenuItemDto } from '../../types/api'
import { archivedOnly } from './categoryGroups'

const coffee: MenuCategoryDto = { id: 'cat-coffee', name: 'Coffee', sortOrder: 1, description: null, loyaltyClubId: null }
const fizz: MenuCategoryDto = { id: 'cat-fizz', name: 'Fruit soda', sortOrder: 4, description: null, loyaltyClubId: null }
const empty: MenuCategoryDto = { id: 'cat-empty', name: 'Seasonal', sortOrder: 5, description: null, loyaltyClubId: null }
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

  it('sends a new category’s description, and shows a saved one under the name', async () => {
    const matcha: MenuCategoryDto = { id: 'cat-matcha', name: 'Houseblend Matcha', sortOrder: 6, description: 'oat milk + ceremonial matcha', loyaltyClubId: null }
    let sent: unknown = null
    server.use(
      http.get(`${API}/menu/categories`, () => HttpResponse.json([coffee, matcha])),
      http.get(`${API}/menu/items`, () => HttpResponse.json([])),
      http.post(`${API}/menu/categories`, async ({ request }) => {
        sent = await request.json()
        return HttpResponse.json({ id: 'cat-new', name: 'Okumidori Matcha', sortOrder: 16, description: 'premium okumidori' })
      }),
    )
    const { user } = renderApp('/menu/categories', signedIn)

    expect(await screen.findByText('oat milk + ceremonial matcha')).toBeInTheDocument()

    await user.type(screen.getByLabelText('New category name'), 'Okumidori Matcha')
    await user.type(screen.getByLabelText('New category description'), '  premium okumidori ')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    await waitFor(() =>
      expect(sent).toEqual({ name: 'Okumidori Matcha', sortOrder: 16, description: 'premium okumidori', loyaltyClubId: null }),
    )
  })

  it('sends a blank description as null', async () => {
    let sent: unknown = null
    server.use(
      http.get(`${API}/menu/categories`, () => HttpResponse.json([coffee])),
      http.post(`${API}/menu/categories`, async ({ request }) => {
        sent = await request.json()
        return HttpResponse.json({ id: 'cat-new', name: 'Refreshers', sortOrder: 11, description: null, loyaltyClubId: null })
      }),
    )
    const { user } = renderApp('/menu/categories', signedIn)

    await user.type(await screen.findByLabelText('New category name'), 'Refreshers')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    await waitFor(() => expect(sent).toEqual({ name: 'Refreshers', sortOrder: 11, description: null, loyaltyClubId: null }))
  })
})
