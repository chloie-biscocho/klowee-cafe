import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { App } from './App'
import { emptyHome, HOME_URL } from './test/fixtures'
import { serveHome, server } from './test/server'

describe('App', () => {
  it('renders every section from the payload', async () => {
    render(<App />)

    expect(await screen.findByRole('heading', { level: 1 })).toHaveTextContent('smol pop-up cafe, big on matcha.')
    for (const name of ['Our story', "what's in the cart", 'packages', "where we're headed", 'bring the cart to your event']) {
      expect(screen.getByRole('heading', { name })).toBeInTheDocument()
    }
    expect(screen.getByRole('contentinfo')).toHaveTextContent('Klowee Cafe · Cagayan de Oro')
  })

  it('shows the error card with nav and footer, and refetches on retry', async () => {
    let calls = 0
    server.use(
      http.get(HOME_URL, () => {
        calls += 1
        return calls === 1 ? new HttpResponse(null, { status: 500 }) : HttpResponse.json(emptyHome)
      }),
    )
    const user = userEvent.setup()
    render(<App />)

    expect(await screen.findByRole('heading', { name: "the cart's offline for a sec, try again?" })).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Main' })).toBeInTheDocument()
    expect(screen.getByRole('contentinfo')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'try again' }))

    expect(await screen.findByText('menu coming soon')).toBeInTheDocument()
    expect(calls).toBe(2)
  })

  it('renders every section with an empty payload, without errors', async () => {
    serveHome(emptyHome)
    render(<App />)

    expect(await screen.findByText('menu coming soon')).toBeInTheDocument()
    expect(screen.getByText('no pop-ups logged yet')).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Placeholder: photo of the cart' })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Placeholder: Klowe & Kee illustration' })).toBeInTheDocument()
    expect(document.getElementById('packages')).toBeNull()
    expect(screen.queryByRole('link', { name: 'packages' })).not.toBeInTheDocument()
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Klowee Cafe')
    expect(screen.queryByRole('link', { name: 'Email us' })).not.toBeInTheDocument()
  })
})
