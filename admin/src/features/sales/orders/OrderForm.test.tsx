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

  it('offers a free drink only to a customer with a reward, and frees the cheapest one', async () => {
    const { user } = renderApp('/sales/orders/new', signedIn)
    await chooseCustomer(user, 'Kee')
    expect(screen.getByRole('radio', { name: 'Free drink' })).toBeDisabled()

    const box = screen.getByRole('combobox', { name: 'Customer' })
    await user.clear(box)
    await user.type(box, 'Prince{Enter}')
    const free = screen.getByRole('radio', { name: 'Free drink' })
    expect(free).toBeEnabled()

    await user.click(screen.getByRole('button', { name: 'One more Spanish Latte' }))
    await user.click(screen.getByRole('button', { name: 'One more Americano' }))
    await user.click(free)
    expect(screen.getByText('Free drink: Americano')).toBeInTheDocument()
    expect(total()).toBe('₱150')
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
      items: [{ menuVersionItemId: officeMenu.items[1]!.id, quantity: 2, addOnIds: [oatMilk.id] }],
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
