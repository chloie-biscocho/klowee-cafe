import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../../test/fixtures'
import { renderApp } from '../../../test/renderApp'
import { kee, keeDetail, prince } from '../../../test/salesFixtures'
import { server } from '../../../test/server'
import type { CustomerDetailDto } from '../../../types/api'

const NEW_URL = 'https://klowee.test/tab/NewNewNewNewNewNewNewNewNewNewNewNewNewNewNew'

describe('Tab link', () => {
  it('copies the link', async () => {
    const { user } = renderApp(`/sales/customers/${kee.id}`, signedIn)

    const box = (await screen.findByRole('heading', { name: 'Tab link' })).parentElement!
    expect(within(box).getByLabelText('Tab link')).toHaveValue(keeDetail.tabUrl)
    expect(within(box).getByText(/Created Nov 8, 2026/)).toBeInTheDocument()

    await user.click(within(box).getByRole('button', { name: 'Copy' }))
    expect(await navigator.clipboard.readText()).toBe(keeDetail.tabUrl)
    expect(await screen.findByText('Link copied.')).toBeInTheDocument()
  })

  it('resets only after the confirm that says the old link stops working', async () => {
    let detail: CustomerDetailDto = keeDetail
    let posts = 0
    server.use(
      http.get(`${API}/customers/:id`, () => HttpResponse.json(detail)),
      http.post(`${API}/customers/:id/tab-link`, () => {
        posts++
        detail = { ...keeDetail, tabUrl: NEW_URL, tabLinkCreatedAt: '2026-11-09T03:00:00Z' }
        return HttpResponse.json({ url: NEW_URL, createdAt: '2026-11-09T03:00:00Z' })
      }),
    )
    const { user } = renderApp(`/sales/customers/${kee.id}`, signedIn)

    await user.click(await screen.findByRole('button', { name: 'Reset link' }))
    const dialog = screen.getByRole('dialog', { name: 'Reset tab link' })
    expect(within(dialog).getByText('The old link stops working at once. Send Kee the new one.')).toBeInTheDocument()

    // Cancel sends nothing.
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    expect(posts).toBe(0)

    await user.click(screen.getByRole('button', { name: 'Reset link' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Reset link' }))

    await waitFor(() => expect(screen.getByLabelText('Tab link')).toHaveValue(NEW_URL))
    expect(posts).toBe(1)
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('without a link offers Create link', async () => {
    let posted = false
    server.use(
      http.get(`${API}/customers/:id`, () =>
        HttpResponse.json(posted ? { ...keeDetail, tabUrl: NEW_URL } : { ...keeDetail, hasTabLink: false, tabUrl: null, tabLinkCreatedAt: null }),
      ),
      http.post(`${API}/customers/:id/tab-link`, () => {
        posted = true
        return HttpResponse.json({ url: NEW_URL, createdAt: '2026-11-09T03:00:00Z' })
      }),
    )
    const { user } = renderApp(`/sales/customers/${kee.id}`, signedIn)

    await user.click(await screen.findByRole('button', { name: 'Create link' }))
    await waitFor(() => expect(screen.getByLabelText('Tab link')).toHaveValue(NEW_URL))
  })
})

describe('Customers list', () => {
  it('marks customers with a link, and creates links for everyone with a balance and no link', async () => {
    const owing = { ...prince, id: 'c0000000-0000-0000-0000-000000000003', name: 'June', nickname: null, outstandingBalance: 150, hasTabLink: false }
    const linked: string[] = []
    server.use(
      http.get(`${API}/customers`, () => HttpResponse.json([kee, prince, owing].map((c) => ({ ...c, hasTabLink: c.hasTabLink || linked.includes(c.id) })))),
      http.post(`${API}/customers/:id/tab-link`, ({ params }) => {
        linked.push(params.id as string)
        return HttpResponse.json({ url: NEW_URL, createdAt: '2026-11-09T03:00:00Z' })
      }),
    )
    const { user } = renderApp('/sales/customers', signedIn)

    const keeRow = (await screen.findByText('Kee')).closest('tr')!
    expect(within(keeRow).getByRole('img', { name: 'Has a tab link' })).toBeInTheDocument()
    expect(within(screen.getByText('Prince').closest('tr')!).queryByRole('img', { name: 'Has a tab link' })).not.toBeInTheDocument()

    // Kee has a link already, Prince owes nothing: only June.
    await user.click(screen.getByRole('button', { name: 'Create links for customers with a balance (1)' }))

    expect(await screen.findByText(/Created 1 link\./)).toBeInTheDocument()
    expect(linked).toEqual([owing.id])
    await waitFor(() => expect(screen.getByRole('button', { name: 'Create links for customers with a balance' })).toBeDisabled())
  })
})
