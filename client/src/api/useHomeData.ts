import { useCallback, useEffect, useState } from 'react'
import type { PublicHome } from './types'

const apiUrl = import.meta.env.VITE_API_URL
if (!apiUrl) {
  throw new Error('VITE_API_URL is not set. Copy client/.env.example to client/.env.')
}
const HOME_URL = `${apiUrl.replace(/\/$/, '')}/api/public/home`

export type HomeState =
  | { status: 'loading'; data: null }
  | { status: 'error'; data: null }
  | { status: 'ready'; data: PublicHome }

/**
 * The page's one request. `useEffect` + `fetch` on purpose — one GET, no
 * mutations, nothing to invalidate; decision 011 explains why the admin app
 * uses RTK Query and this does not.
 *
 * `attempt` is the retry lever: bumping it re-runs the effect. Each run owns an
 * AbortController, so a superseded request (StrictMode's double mount, a retry
 * mid-flight, unmounting) is cancelled and can never set state late.
 */
export function useHomeData(): HomeState & { retry: () => void } {
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<HomeState>({ status: 'loading', data: null })

  useEffect(() => {
    const controller = new AbortController()

    fetch(HOME_URL, { signal: controller.signal, headers: { accept: 'application/json' } })
      .then((response) => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`)
        return response.json() as Promise<PublicHome>
      })
      .then((data) => setState({ status: 'ready', data }))
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        console.error('Could not load /api/public/home', error)
        setState({ status: 'error', data: null })
      })

    return () => controller.abort()
  }, [attempt])

  const retry = useCallback(() => {
    setState({ status: 'loading', data: null })
    setAttempt((count) => count + 1)
  }, [])

  return { ...state, retry }
}
