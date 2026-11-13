import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { coffeeClub } from '../../test/salesFixtures'
import { server } from '../../test/server'
import type { LoyaltyClubRequest, MenuCategoryDto } from '../../types/api'

const coffee: MenuCategoryDto = { id: 'cat-coffee', name: 'Coffee', sortOrder: 1, description: null, loyaltyClubId: coffeeClub.id }
const signature: MenuCategoryDto = { id: 'cat-signature', name: 'Signature Coffee', sortOrder: 2, description: null, loyaltyClubId: null }

describe('Loyalty cards', () => {
  it('lists the cards with their state, rewards and members', async () => {
    server.use(http.get(`${API}/menu/categories`, () => HttpResponse.json([coffee, signature])))
    renderApp('/menu/loyalty-cards', signedIn)

    const row = (await screen.findByText('The Coffee Club')).closest('tr')!
    expect(within(row).getByText('Active')).toBeInTheDocument()
    expect(within(row).getByText('5: 50% OFF · 8: EXTRA SHOT · 12: FREE DRINK')).toBeInTheDocument()
    expect(within(row).getByText('Coffee')).toBeInTheDocument()
    expect(within(screen.getByText('The Matcha Club').closest('tr')!).getByText('Not active')).toBeInTheDocument()
  })

  it('saves edited rewards and the categories that feed the card', async () => {
    let sent: LoyaltyClubRequest | null = null
    server.use(
      http.get(`${API}/menu/categories`, () => HttpResponse.json([coffee, signature])),
      http.put(`${API}/loyalty/clubs/:id`, async ({ request }) => {
        sent = (await request.json()) as LoyaltyClubRequest
        return HttpResponse.json(coffeeClub)
      }),
    )
    const { user } = renderApp('/menu/loyalty-cards', signedIn)
    const row = (await screen.findByText('The Coffee Club')).closest('tr')!
    await user.click(within(row).getByRole('button', { name: 'Edit' }))

    const dialog = await screen.findByRole('dialog')
    const percent = within(dialog).getByLabelText('Reward 1 percent')
    await user.clear(percent)
    await user.type(percent, '40')
    const label = within(dialog).getByLabelText('Reward 1 label')
    await user.clear(label)
    await user.type(label, '40% OFF')
    await user.click(within(dialog).getByLabelText('Signature Coffee'))
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(sent).not.toBeNull())
    expect(sent!.rewards).toEqual([
      { slot: 5, type: 'PercentOff', percent: 40, addOnId: null, label: '40% OFF' },
      { slot: 8, type: 'FreeAddOn', percent: null, addOnId: coffeeClub.rewards[1]!.addOnId, label: 'EXTRA SHOT' },
      { slot: 12, type: 'FreeDrink', percent: null, addOnId: null, label: 'FREE DRINK' },
    ])
    expect(sent!.categoryIds).toEqual(['cat-coffee', 'cat-signature'])
    expect(sent!.isActive).toBe(true)
  })

  it('refuses a free add-on reward without its add-on, before saving', async () => {
    let calls = 0
    server.use(
      http.put(`${API}/loyalty/clubs/:id`, () => {
        calls++
        return HttpResponse.json(coffeeClub)
      }),
    )
    const { user } = renderApp('/menu/loyalty-cards', signedIn)
    const row = (await screen.findByText('The Coffee Club')).closest('tr')!
    await user.click(within(row).getByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog')

    await user.selectOptions(within(dialog).getByLabelText('Reward 2 add-on'), '')
    await user.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText('Choose the add-on.')).toBeInTheDocument()
    expect(calls).toBe(0)
  })
})
