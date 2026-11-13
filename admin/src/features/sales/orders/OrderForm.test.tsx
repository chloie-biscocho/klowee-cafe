import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, signedIn } from '../../../test/fixtures'
import { renderApp } from '../../../test/renderApp'
import { kee, oatMilk, officeMenu, unpaidOrder } from '../../../test/salesFixtures'
import { problem, server } from '../../../test/server'
import type { OrderRequest } from '../../../types/api'

const total = () => screen.getByLabelText('Total').textContent

async function chooseCustomer(user: ReturnType<typeof renderApp>['user'], name: string) {
  const box = await screen.findByRole('combobox', { name: 'Customer' })
  await user.click(box)
  await user.type(box, name)
  await user.keyboard('{Enter}')
}

describe('Order form', () => {
  it('updates the live total as drinks and add-ons are chosen, from the keyboard', async () => {
    const { user } = renderApp('/sales/orders/new', signedIn)
    await chooseCustomer(user, 'Kee')

    // Choosing a customer moves focus to the drink filter: type, Enter adds one.
    const filter = screen.getByRole('textbox', { name: 'Find a drink' })
    await waitFor(() => expect(filter).toHaveFocus())
    await user.type(filter, 'span{Enter}{Enter}')
    expect(total()).toBe('₱300')

    await user.click(within(screen.getByRole('listitem', { name: 'Spanish Latte' })).getByLabelText(/Oat milk/))
    expect(total()).toBe('₱360')

    // Escape clears the filter, so the whole menu is back.
    await user.click(filter)
    await user.keyboard('{Escape}')
    await user.click(screen.getByRole('button', { name: 'One more Americano' }))
    expect(total()).toBe('₱470')
  })

  it('offers a reward only where the customer has one for the drink’s card, and it changes the total', async () => {
    const { user } = renderApp('/sales/orders/new', signedIn)
    await chooseCustomer(user, 'Kee')
    expect(screen.queryByRole('radio', { name: 'Free drink' })).not.toBeInTheDocument()   // no order-level free drink any more
    expect(screen.getByText(/Coffee 4\/12 · 1 reward/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'One more Spanish Latte' }))
    await user.click(screen.getByRole('button', { name: 'One more Strawberry Milk' }))
    expect(total()).toBe('₱290')

    // Kee has the 50% off ready; a Coffee Club drink can carry it, Strawberry Milk (no card) can't.
    const latte = within(screen.getByRole('listitem', { name: 'Spanish Latte' }))
    expect(latte.getAllByRole('switch').map((s) => s.textContent)).toEqual(['★ 50% off, Coffee Club'])
    expect(within(screen.getByRole('listitem', { name: 'Strawberry Milk' })).queryByRole('switch')).not.toBeInTheDocument()

    await user.click(latte.getByRole('switch', { name: /50% off/ }))
    expect(total()).toBe('₱215')                       // half of the 150 latte off
    expect(screen.getByText('★ 50% OFF −₱75')).toBeInTheDocument()

    // Off again: back to full price.
    await user.click(latte.getByRole('switch', { name: /50% off/ }))
    expect(total()).toBe('₱290')
  })

  it('lets one copy of a reward go on one line only, and frees only the drink of a free drink', async () => {
    const { user } = renderApp('/sales/orders/new', signedIn)
    await chooseCustomer(user, 'Prince')
    await user.click(screen.getByRole('button', { name: 'One more Spanish Latte' }))
    await user.click(screen.getByRole('button', { name: 'One more Americano' }))
    const latte = within(screen.getByRole('listitem', { name: 'Spanish Latte' }))
    const americano = within(screen.getByRole('listitem', { name: 'Americano' }))
    await user.click(latte.getByLabelText(/Oat milk/))
    expect(total()).toBe('₱290')

    // Prince's 50% off is used; the extra shot and the free drink are ready.
    expect(latte.getAllByRole('switch').map((s) => s.textContent)).toEqual(['★ extra shot, Coffee Club', '★ free drink, Coffee Club'])

    await user.click(latte.getByRole('switch', { name: /free drink/ }))
    expect(total()).toBe('₱140')                        // the latte free, its oat milk (30) still charged
    expect(americano.getByRole('switch', { name: /free drink/ })).toBeDisabled()
    expect(americano.getByRole('switch', { name: /extra shot/ })).toBeEnabled()
  })

  it('sends the reward on its line', async () => {
    let sent: OrderRequest | null = null
    server.use(
      http.post(`${API}/orders`, async ({ request }) => {
        sent = (await request.json()) as OrderRequest
        return HttpResponse.json({ ...unpaidOrder, id: 'o-new' }, { status: 201 })
      }),
    )
    const { user, router } = renderApp('/sales/orders/new', signedIn)
    await chooseCustomer(user, 'Kee')
    await user.click(screen.getByRole('button', { name: 'One more Americano' }))
    await user.click(within(screen.getByRole('listitem', { name: 'Americano' })).getByRole('switch', { name: /50% off/ }))
    await user.click(screen.getByRole('button', { name: 'Save order' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/sales/orders/o-new'))
    expect(sent!.items).toEqual([{ menuVersionItemId: officeMenu.items[0]!.id, quantity: 1, addOnIds: [], rewardId: 'r-half' }])
  })

  it('sends the order the screen shows', async () => {
    let sent: OrderRequest | null = null
    server.use(
      http.post(`${API}/orders`, async ({ request }) => {
        sent = (await request.json()) as OrderRequest
        return HttpResponse.json({ ...unpaidOrder, id: 'o-new' }, { status: 201 })
      }),
    )
    const { user, router } = renderApp('/sales/orders/new', signedIn)
    await chooseCustomer(user, 'Kee')
    await user.click(screen.getByRole('button', { name: 'One more Spanish Latte' }))
    await user.click(screen.getByRole('button', { name: 'One more Spanish Latte' }))
    await user.click(within(screen.getByRole('listitem', { name: 'Spanish Latte' })).getByLabelText(/Oat milk/))
    await user.click(screen.getByRole('radio', { name: 'Percent' }))
    await user.type(screen.getByLabelText(/Percent off/), '10')

    await user.click(screen.getByRole('button', { name: 'Save order' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/sales/orders/o-new'))
    expect(sent).toMatchObject({
      customerId: kee.id,
      payerId: null,
      discountType: 'Percent',
      discountValue: 10,
      items: [{ menuVersionItemId: officeMenu.items[1]!.id, quantity: 2, addOnIds: [oatMilk.id], rewardId: null }],
    })
    expect(sent!.orderedOn).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })

  it('blocks order entry until an office menu is published', async () => {
    server.use(
      http.get(`${API}/menu/versions/current`, () => problem(404, 'Not found', 'No published Office menu is effective.')),
    )
    renderApp('/sales/orders/new', signedIn)

    expect(await screen.findByText(/Publish an office menu version first/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Go to menu versions' })).toHaveAttribute('href', '/menu/versions')
    expect(screen.queryByRole('combobox', { name: 'Customer' })).not.toBeInTheDocument()
  })
})
