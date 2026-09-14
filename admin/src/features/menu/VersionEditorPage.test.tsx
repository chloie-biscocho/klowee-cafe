import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { MenuVersionItemRequest } from '../../types/api'
import { API, draftVersionDetail, signedIn } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { server } from '../../test/server'

describe('VersionEditorPage', () => {
  it('sends the whole item list, with the edited price, on save', async () => {
    let sent: MenuVersionItemRequest[] | null = null

    server.use(
      http.put(`${API}/menu/versions/:id/items`, async ({ request }) => {
        sent = (await request.json()) as MenuVersionItemRequest[]
        return HttpResponse.json(draftVersionDetail)
      }),
    )

    const { user } = renderApp(`/menu/versions/${draftVersionDetail.id}`, signedIn)

    const price = await screen.findByLabelText('Price for Ube Latte')
    await user.clear(price)
    await user.type(price, '175')

    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() => expect(sent).not.toBeNull())

    // A full replacement: both items go, not just the one that changed.
    expect(sent).toEqual([
      {
        menuItemId: draftVersionDetail.items[0]!.menuItemId,
        price: 175,
        isAvailable: true,
        isFeatured: false,
        sortOrder: 0,
      },
      {
        menuItemId: draftVersionDetail.items[1]!.menuItemId,
        price: 165,
        isAvailable: true,
        isFeatured: true,
        sortOrder: 1,
      },
    ])

    expect(await screen.findByText('Menu saved.')).toBeInTheDocument()
  })

  it('is read-only once the version is published', async () => {
    server.use(
      http.get(`${API}/menu/versions/:id`, () =>
        HttpResponse.json({ ...draftVersionDetail, isPublished: true }),
      ),
    )

    renderApp(`/menu/versions/${draftVersionDetail.id}`, signedIn)

    expect(await screen.findByRole('button', { name: 'Copy to new draft' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Price for Ube Latte')).not.toBeInTheDocument()
    expect(screen.getByText('₱150')).toBeInTheDocument()
  })
})
