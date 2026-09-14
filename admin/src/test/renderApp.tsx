import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactElement } from 'react'
import { Provider } from 'react-redux'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { routes } from '../app/router'
import { makeStore, type RootState } from '../app/store'
import { ToastViewport } from '../features/toasts/ToastViewport'

/**
 * Mounts the real route tree in a memory router with a fresh store, so a test
 * exercises what the browser would: the same guards, the same shell, the same
 * RTK Query cache lifetime.
 */
export function renderApp(route = '/', preloadedState?: Partial<RootState>) {
  const store = makeStore(preloadedState)
  const router = createMemoryRouter(routes, { initialEntries: [route] })

  render(
    <Provider store={store}>
      <RouterProvider router={router} />
      <ToastViewport />
    </Provider>,
  )

  return { store, router, user: userEvent.setup() }
}

/** For a component rendered on its own, outside the router. */
export function renderWithStore(ui: ReactElement, preloadedState?: Partial<RootState>) {
  const store = makeStore(preloadedState)
  render(<Provider store={store}>{ui}</Provider>)
  return { store, user: userEvent.setup() }
}
