import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn, siteSettings } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { server } from '../../test/server'
import type { PaymentMethodDto, PaymentMethodRequest, SiteSettingRequest } from '../../types/api'

const gcash: PaymentMethodDto = {
  id: 'pm1', channel: 'GCash', label: 'GCash', accountName: 'Klowee Cafe', accountNumber: '0917 000 0000',
  qrUrl: null, isActive: true, sortOrder: 0,
}

describe('Settings: customer payments', () => {
  it('adds a way to pay, then lists it', async () => {
    let methods: PaymentMethodDto[] = []
    let sent: PaymentMethodRequest | null = null
    server.use(
      http.get(`${API}/payment-methods`, () => HttpResponse.json(methods)),
      http.post(`${API}/payment-methods`, async ({ request }) => {
        sent = (await request.json()) as PaymentMethodRequest
        methods = [{ ...sent, id: 'pm2' }]
        return HttpResponse.json(methods[0], { status: 201 })
      }),
    )
    const { user } = renderApp('/site/settings', signedIn)

    expect(await screen.findByText(/No ways to pay yet/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Add a way to pay' }))
    const dialog = screen.getByRole('dialog', { name: 'New way to pay' })
    // Cash is paid in person, not offered here.
    expect(within(dialog).queryByRole('option', { name: 'Cash' })).not.toBeInTheDocument()

    await user.click(within(dialog).getByRole('button', { name: 'Save' }))
    expect(await within(dialog).findByText(/Name it as customers will see it/)).toBeInTheDocument()

    await user.selectOptions(within(dialog).getByLabelText('Channel'), 'CIMB')
    await user.type(within(dialog).getByLabelText('Label'), 'CIMB Bank')
    await user.type(within(dialog).getByLabelText('Account name'), 'Chloie')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(sent).toEqual({
      channel: 'CIMB', label: 'CIMB Bank', accountName: 'Chloie', accountNumber: null, qrUrl: null, isActive: true, sortOrder: 0,
    })
    expect(await screen.findByText('CIMB Bank')).toBeInTheDocument()
  })

  it('hides a method, and saves the page note on its own', async () => {
    let active = true
    let settingsSent: SiteSettingRequest[] | null = null
    server.use(
      http.get(`${API}/payment-methods`, () => HttpResponse.json([{ ...gcash, isActive: active }])),
      http.patch(`${API}/payment-methods/:id/deactivate`, () => {
        active = false
        return HttpResponse.json({ ...gcash, isActive: false })
      }),
      http.put(`${API}/settings`, async ({ request }) => {
        settingsSent = (await request.json()) as SiteSettingRequest[]
        return HttpResponse.json([...siteSettings, ...settingsSent])
      }),
    )
    const { user } = renderApp('/site/settings', signedIn)

    const row = (await screen.findByText('Klowee Cafe · 0917 000 0000')).closest('li')!
    expect(within(row).getByText('Shown')).toBeInTheDocument()
    await user.click(within(row).getByRole('button', { name: 'Hide' }))
    expect(await within(row).findByText('Hidden')).toBeInTheDocument()

    expect(screen.getByRole('button', { name: 'Save note' })).toBeDisabled()
    await user.type(screen.getByLabelText('Note on the tab page'), 'Exact amount, please.')
    await user.click(screen.getByRole('button', { name: 'Save note' }))
    await waitFor(() => expect(settingsSent).toEqual([{ key: 'tab_page_note', value: 'Exact amount, please.' }]))
    // The homepage form is untouched.
    expect(screen.getByRole('button', { name: 'Save settings' })).toBeDisabled()
  })
})
