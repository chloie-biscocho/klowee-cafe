import { screen, within } from '@testing-library/react'
import { http } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { problem, server } from '../../test/server'

const REFERENCED =
  "Menu item 'Ube Latte' is used by 3 menu version item(s). Archive it instead."

describe('ItemsPage', () => {
  it('shows the API 409 message in a toast when a referenced item cannot be deleted', async () => {
    server.use(
      http.delete(`${API}/menu/items/:id`, () => problem(409, 'Conflict', REFERENCED)),
    )

    const { user } = renderApp('/menu/items', signedIn)

    await user.click(await screen.findByRole('button', { name: 'Delete' }))

    const dialog = await screen.findByRole('dialog')
    await user.click(within(dialog).getByRole('button', { name: 'Delete' }))

    expect(await screen.findByText(REFERENCED)).toBeInTheDocument()
  })
})
