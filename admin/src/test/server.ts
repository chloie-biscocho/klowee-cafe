import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import {
  API,
  corporatePackage,
  draftVersion,
  draftVersionDetail,
  eventSummary,
  menuItems,
  owner,
  publishedVersion,
  siteSettings,
  upcomingEvent,
} from './fixtures'

/**
 * The happy path for every endpoint a screen touches on load. A test that cares
 * about a specific response overrides just that one with `server.use(...)`.
 */
export const handlers = [
  http.get(`${API}/auth/me`, () => HttpResponse.json(owner)),
  http.get(`${API}/menu/categories`, () => HttpResponse.json([])),
  http.get(`${API}/menu/add-ons`, () => HttpResponse.json([])),
  http.get(`${API}/menu/items`, () => HttpResponse.json(menuItems)),
  http.get(`${API}/menu/versions`, () => HttpResponse.json([draftVersion, publishedVersion])),
  http.get(`${API}/menu/versions/:id`, () => HttpResponse.json(draftVersionDetail)),
  http.get(`${API}/packages`, () => HttpResponse.json([corporatePackage])),
  http.get(`${API}/events`, () => HttpResponse.json([eventSummary(upcomingEvent)])),
  http.get(`${API}/events/:id`, () => HttpResponse.json(upcomingEvent)),
  http.get(`${API}/announcements`, () => HttpResponse.json([])),
  http.get(`${API}/settings`, () => HttpResponse.json(siteSettings)),
]

export const server = setupServer(...handlers)

/** RFC 7807, the shape every API failure arrives in. */
export function problem(status: number, title: string, detail: string) {
  return HttpResponse.json({ status, title, detail }, { status })
}
