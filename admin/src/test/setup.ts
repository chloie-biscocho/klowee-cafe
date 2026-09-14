import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from './server'

// `error` on an unhandled request: a call the tests did not plan for should
// fail loudly rather than hang or silently hit the network.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))

afterEach(() => {
  server.resetHandlers()
  cleanup()
  window.localStorage.clear()
})

afterAll(() => server.close())
