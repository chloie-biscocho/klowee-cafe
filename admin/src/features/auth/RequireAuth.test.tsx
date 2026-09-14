import { screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderApp } from '../../test/renderApp'

describe('RequireAuth', () => {
  it('sends an unauthenticated visitor to /login', async () => {
    const { router } = renderApp('/menu/versions')

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/login')
    })

    expect(screen.getByText('Sign in to manage the menu.')).toBeInTheDocument()
  })

  it('remembers where they were heading', async () => {
    const { router } = renderApp('/menu/items')

    await waitFor(() => {
      expect(router.state.location.state).toEqual({ from: '/menu/items' })
    })
  })
})
