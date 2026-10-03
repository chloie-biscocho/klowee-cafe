import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { SiteSettingRequest } from '../../types/api'
import { API, signedIn, siteSettings } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { server } from '../../test/server'

describe('SettingsPage', () => {
  it('sends only the keys that changed', async () => {
    let sent: SiteSettingRequest[] | null = null
    server.use(
      http.put(`${API}/settings`, async ({ request }) => {
        sent = (await request.json()) as SiteSettingRequest[]
        return HttpResponse.json(siteSettings)
      }),
    )

    const { user } = renderApp('/site/settings', signedIn)

    const save = await screen.findByRole('button', { name: 'Save settings' })
    expect(save).toBeDisabled()

    const ticker = screen.getByLabelText('Text')
    await user.clear(ticker)
    await user.type(ticker, 'Now booking December')
    // A field typed back to its saved value is not a change.
    const heading = screen.getAllByLabelText('Heading')[0]!
    await user.type(heading, '!')
    await user.type(heading, '{Backspace}')

    expect(save).toBeEnabled()
    await user.click(save)

    await waitFor(() => expect(sent).not.toBeNull())
    expect(sent).toEqual([{ key: 'ticker_fallback', value: 'Now booking December' }])
    expect(await screen.findByText('Settings saved.')).toBeInTheDocument()
  })
})
