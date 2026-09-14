import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { API, loginResponse } from '../../test/fixtures'
import { renderApp } from '../../test/renderApp'
import { problem, server } from '../../test/server'

describe('LoginPage', () => {
  it('shows the API message when the credentials are wrong', async () => {
    server.use(
      http.post(`${API}/auth/login`, () =>
        problem(401, 'Unauthorized', 'Invalid email or password.'),
      ),
    )

    const { user, store } = renderApp('/login')

    await user.type(screen.getByLabelText('Email'), 'owner@klowee.test')
    await user.type(screen.getByLabelText('Password'), 'wrong-password')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Invalid email or password.')).toBeInTheDocument()
    expect(store.getState().auth.token).toBeNull()
  })

  it('stores the token and redirects on success', async () => {
    server.use(http.post(`${API}/auth/login`, () => HttpResponse.json(loginResponse)))

    const { user, store, router } = renderApp('/login')

    await user.type(screen.getByLabelText('Email'), 'klowe.cafe@gmail.com')
    await user.type(screen.getByLabelText('Password'), 'correct-horse')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    await waitFor(() => {
      expect(store.getState().auth.token).toBe(loginResponse.accessToken)
    })

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/menu/versions')
    })

    // ...and the session survives a reload.
    expect(window.localStorage.getItem('klowee.admin.auth')).toContain(loginResponse.accessToken)
  })
})
