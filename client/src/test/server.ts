import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import type { PublicHome } from '../api/types'
import { fullHome, HOME_URL } from './fixtures'

export const server = setupServer(http.get(HOME_URL, () => HttpResponse.json(fullHome)))

/** Serves one payload for the rest of a test. */
export function serveHome(body: PublicHome) {
  server.use(http.get(HOME_URL, () => HttpResponse.json(body)))
}
