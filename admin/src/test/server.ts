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
import { keeDetail, kee, monthly, oatMilk, officeMenu, outstanding, prince, summary, unpaidOrder } from './salesFixtures'

/**
 * The happy path for every endpoint a screen touches on load. A test that cares
 * about a specific response overrides just that one with `server.use(...)`.
 */
export const handlers = [
  http.get(`${API}/auth/me`, () => HttpResponse.json(owner)),
  http.get(`${API}/menu/categories`, () => HttpResponse.json([])),
  http.get(`${API}/menu/add-ons`, () => HttpResponse.json([oatMilk])),
  http.get(`${API}/menu/items`, () => HttpResponse.json(menuItems)),
  http.get(`${API}/menu/versions`, () => HttpResponse.json([draftVersion, publishedVersion])),
  http.get(`${API}/menu/versions/current`, () => HttpResponse.json(officeMenu)),
  http.get(`${API}/menu/versions/:id`, () => HttpResponse.json(draftVersionDetail)),
  http.get(`${API}/packages`, () => HttpResponse.json([corporatePackage])),
  http.get(`${API}/events`, () => HttpResponse.json([eventSummary(upcomingEvent)])),
  http.get(`${API}/events/:id`, () => HttpResponse.json(upcomingEvent)),
  http.get(`${API}/announcements`, () => HttpResponse.json([])),
  http.get(`${API}/settings`, () => HttpResponse.json(siteSettings)),
  http.get(`${API}/customers`, () => HttpResponse.json([kee, prince])),
  http.get(`${API}/customers/:id`, () => HttpResponse.json(keeDetail)),
  http.get(`${API}/customers/:id/loyalty`, () => HttpResponse.json([])),
  http.get(`${API}/orders`, () => HttpResponse.json({ items: [], page: 1, pageSize: 25, totalCount: 0 })),
  http.get(`${API}/orders/:id`, () => HttpResponse.json(unpaidOrder)),
  http.get(`${API}/reports/summary`, () => HttpResponse.json(summary)),
  http.get(`${API}/reports/outstanding`, () => HttpResponse.json(outstanding)),
  http.get(`${API}/reports/monthly`, ({ request }) => {
    const params = new URL(request.url).searchParams
    return HttpResponse.json(monthly(params.get('from'), params.get('to')!))
  }),
]

export const server = setupServer(...handlers)

/** RFC 7807, the shape every API failure arrives in. */
export function problem(status: number, title: string, detail: string) {
  return HttpResponse.json({ status, title, detail }, { status })
}
