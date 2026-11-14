import { useCallback, useEffect, useRef, useState } from 'react'
import type { ClaimRequest, PublicTab, TabClaim } from './types'

const apiUrl = import.meta.env.VITE_API_URL
if (!apiUrl) {
  throw new Error('VITE_API_URL is not set. Copy client/.env.example to client/.env.')
}

export const tabUrl = (token: string) => `${apiUrl.replace(/\/$/, '')}/api/public/tab/${encodeURIComponent(token)}`

export type TabState =
  | { status: 'loading'; data: null }
  | { status: 'error'; data: null }
  /** The link was replaced or removed (any 404): ask Klowee for a new one. */
  | { status: 'gone'; data: null }
  | { status: 'ready'; data: PublicTab }

/** A write's outcome: the API's own words when it says no. */
export type WriteResult = { ok: true } | { ok: false; message: string }

const NETWORK_MESSAGE = "Couldn't reach Klowee. Check your connection and try again."

async function problemMessage(response: Response): Promise<string> {
  if (response.status === 429) return 'Too many tries for now. Please wait a little and try again.'
  if (response.status === 404) return "This link isn't active anymore."
  try {
    const body = (await response.json()) as { detail?: string; errors?: Record<string, string[]> }
    return body.detail ?? Object.values(body.errors ?? {})[0]?.[0] ?? 'Something went wrong. Please try again.'
  } catch {
    return 'Something went wrong. Please try again.'
  }
}

/** One read of the page: ready, or gone for any 404 (a replaced or removed link). */
async function fetchTab(token: string, signal: AbortSignal): Promise<TabState> {
  const response = await fetch(tabUrl(token), { signal, headers: { accept: 'application/json' } })
  if (response.status === 404) return { status: 'gone', data: null }
  if (!response.ok) throw new Error(`HTTP ${response.status}`)
  return { status: 'ready', data: (await response.json()) as PublicTab }
}

/**
 * The tab page's data, the same three-state pattern as useHomeData (decision
 * 011) plus a fourth for a dead link. It also makes the client's first writes,
 * with plain fetch: `submitClaim` and `requestCard` post, then refetch quietly
 * (the page stays on screen while the fresh copy loads).
 */
export function useTab(token: string) {
  const [attempt, setAttempt] = useState(0)
  // No token at all ("/tab/"): a dead link from the start, nothing to fetch.
  const [state, setState] = useState<TabState>(() =>
    token === '' ? { status: 'gone', data: null } : { status: 'loading', data: null },
  )
  // The latest request, so a slow older response can never overwrite a newer one.
  const latest = useRef<AbortController | null>(null)

  /**
   * Fetches the page into state. `quiet` (a refresh after a write) keeps
   * what's on screen if it fails. Each call aborts the one before, so a slow
   * older response can never overwrite a newer one.
   */
  const load = useCallback(
    (quiet: boolean): Promise<void> => {
      latest.current?.abort()
      const controller = new AbortController()
      latest.current = controller
      return fetchTab(token, controller.signal)
        .then((next) => {
          if (!controller.signal.aborted) setState(next)
        })
        .catch((error: unknown) => {
          if (controller.signal.aborted) return
          console.error('Could not load the tab', error instanceof Error ? error.message : error)
          if (!quiet) setState({ status: 'error', data: null })
        })
    },
    [token],
  )

  useEffect(() => {
    if (token === '') return
    const controller = new AbortController()
    latest.current = controller
    fetchTab(token, controller.signal)
      .then(setState)
      .catch((error: unknown) => {
        if (controller.signal.aborted) return
        console.error('Could not load the tab', error instanceof Error ? error.message : error)
        setState({ status: 'error', data: null })
      })
    return () => controller.abort()
  }, [token, attempt])

  const retry = useCallback(() => {
    setState({ status: 'loading', data: null })
    setAttempt((count) => count + 1)
  }, [])

  const post = useCallback(
    async (path: string, body?: unknown, quick?: (response: Response) => Promise<void>): Promise<WriteResult> => {
      let response: Response
      try {
        response = await fetch(`${tabUrl(token)}${path}`, {
          method: 'POST',
          headers: { accept: 'application/json', ...(body ? { 'content-type': 'application/json' } : {}) },
          body: body ? JSON.stringify(body) : undefined,
        })
      } catch {
        return { ok: false, message: NETWORK_MESSAGE }
      }
      if (!response.ok) return { ok: false, message: await problemMessage(response) }
      if (quick) {
        // Show the API's answer now; the full refresh follows in the background.
        await quick(response)
        void load(true)
      } else {
        await load(true)
      }
      return { ok: true }
    },
    [token, load],
  )

  /**
   * "I've paid": reports a transfer for Klowee to confirm. The new claim shows
   * at once (the API returns it), then the page refetches quietly.
   */
  const submitClaim = useCallback(
    (claim: ClaimRequest) =>
      post('/claims', claim, async (response) => {
        const created = (await response.json()) as TabClaim
        setState((current) =>
          current.status === 'ready'
            ? {
                status: 'ready',
                data: {
                  ...current.data,
                  claims: [created, ...current.data.claims],
                  pendingClaimsTotal: current.data.pendingClaimsTotal + created.amount,
                },
              }
            : current,
        )
      }),
    [post],
  )

  /** "Ask for a card", then refreshes the page. */
  const requestCard = useCallback((clubId: string) => post(`/clubs/${clubId}/request`), [post])

  return { ...state, retry, submitClaim, requestCard }
}
