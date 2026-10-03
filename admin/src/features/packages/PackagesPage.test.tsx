import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { PackageRequest } from '../../types/api'
import { API, corporatePackage, signedIn } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { server } from '../../test/server'

describe('PackagesPage', () => {
  it('sends the whole package with inclusions in the edited order', async () => {
    let sent: PackageRequest | null = null
    server.use(
      http.put(`${API}/packages/:id`, async ({ request }) => {
        sent = (await request.json()) as PackageRequest
        return HttpResponse.json(corporatePackage)
      }),
    )

    const { user } = renderApp('/site/packages', signedIn)

    await user.click(await screen.findByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog', { name: 'Edit package' })

    // "60 cups" to the top, then drop "3 hours of service".
    await user.click(within(dialog).getByRole('button', { name: 'Move inclusion 3 up' }))
    await user.click(within(dialog).getByRole('button', { name: 'Move inclusion 2 up' }))
    await user.click(within(dialog).getByRole('button', { name: 'Remove inclusion 3' }))
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(sent).not.toBeNull())
    expect(sent!.name).toBe('Corporate cart')
    expect(sent!.inclusions).toEqual([
      { text: '60 cups', sortOrder: 0 },
      { text: '2 baristas', sortOrder: 1 },
    ])
    expect(await screen.findByText('Package updated.')).toBeInTheDocument()
  })
})
