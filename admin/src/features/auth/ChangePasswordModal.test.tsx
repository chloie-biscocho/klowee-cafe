import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { problem, server } from '../../test/server'

async function openModal() {
  const app = renderApp('/', signedIn)
  await app.user.click(await screen.findByRole('button', { name: 'Change password' }))
  const dialog = screen.getByRole('dialog', { name: 'Change password' })
  const fill = async (current: string, next: string, confirm = next) => {
    await app.user.type(within(dialog).getByLabelText('Current password'), current)
    await app.user.type(within(dialog).getByLabelText('New password'), next)
    await app.user.type(within(dialog).getByLabelText('Confirm new password'), confirm)
    await app.user.click(within(dialog).getByRole('button', { name: 'Change password' }))
  }
  return { ...app, dialog, fill }
}

describe('Change password', () => {
  it('sends the current and new password, and closes on success', async () => {
    let sent: unknown = null
    server.use(
      http.put(`${API}/auth/password`, async ({ request }) => {
        sent = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { fill } = await openModal()

    await fill('old-password-1', 'a-new-long-password')

    expect(await screen.findByText('Password changed.')).toBeInTheDocument()
    expect(sent).toEqual({ currentPassword: 'old-password-1', newPassword: 'a-new-long-password' })
    expect(screen.queryByRole('dialog', { name: 'Change password' })).not.toBeInTheDocument()
  })

  it("shows the API's 401 message and keeps the owner signed in", async () => {
    server.use(http.put(`${API}/auth/password`, () => problem(401, 'Unauthorized', 'The current password is incorrect.')))
    const { fill, store, dialog } = await openModal()

    await fill('wrong-password', 'a-new-long-password')

    expect(await screen.findByText('The current password is incorrect.')).toBeInTheDocument()
    expect(store.getState().auth.token).not.toBeNull()
    expect(dialog).toBeInTheDocument()
  })

  it('checks length and the confirmation before sending anything', async () => {
    let called = false
    server.use(http.put(`${API}/auth/password`, () => { called = true; return new HttpResponse(null, { status: 204 }) }))
    const { fill } = await openModal()

    await fill('old-password-1', 'short', 'shorter')

    expect(await screen.findByText('Use at least 12 characters.')).toBeInTheDocument()
    expect(screen.getByText("The two new passwords don't match.")).toBeInTheDocument()
    expect(called).toBe(false)
  })
})
