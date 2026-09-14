# Architecture

Klowee Cafe is a monorepo with three deployable apps plus a shared PostgreSQL database.

## The three apps

- **`api/` — ASP.NET Core Web API (EF Core + PostgreSQL).** The single source of
  truth for data and business rules. Exposes a JSON HTTP API. Owns the database
  schema through EF Core migrations.
- **`admin/` — React + Vite + TypeScript + Redux Toolkit.** A private dashboard the
  two co-owners use to manage the menu, packages, events, announcements and site
  settings. Redux Toolkit holds client state and cached server data.
- **`client/` — React + Vite + TypeScript.** The public marketing site. Read-mostly:
  shows the current menu, packages, upcoming events and announcements.

## How they talk

```
client (browser)  ─┐
                   ├─►  HTTPS / JSON  ─►  api (ASP.NET Core)  ─►  EF Core  ─►  PostgreSQL (Supabase)
admin  (browser)  ─┘
```

Both frontends are plain browser SPAs that call the API over HTTPS with JSON. There
is no direct database access from the browser — everything goes through the API. The
API talks to PostgreSQL (Supabase) via EF Core with the Npgsql provider.

Only the `admin/` app requires a signed-in Owner. `client/` stays anonymous and
read-only, reaching the API through the handful of endpoints marked public.

## Request lifecycle

Every request takes the same path through the API:

```
browser  ─►  [ CORS ]  ─►  [ Authentication ]  ─►  [ Authorization ]  ─►  Controller
                              validates the JWT      fallback policy:      HTTP only:
                              signature, issuer,     authenticated user    binds + validates
                              audience, expiry;      required unless       the request, returns
                              fills HttpContext.User [AllowAnonymous]      a DTO

                                    ─►  Service  ─►  DbContext  ─►  PostgreSQL (Supabase)
                                        business        LINQ → SQL,     the data
                                        rules,          soft-delete
                                        transactions    filter, audit
                                                        fields
```

- **Authentication** happens before anything application-specific. The JWT is
  verified against the signing key the API itself used to issue it, so no
  database lookup is needed to know who is calling. See
  `docs/decisions/004-jwt-auth.md`.
- **Authorization** uses a *fallback policy*: any endpoint that does not state
  its own requirements needs an authenticated user. Public endpoints
  (`GET /api/health`, `POST /api/auth/login`, `GET /api/menu/versions/current`)
  opt out with `[AllowAnonymous]`, so a new endpoint is closed by default rather
  than open by default.
- **Controllers** (`api/Klowee.Api/Controllers/`) do HTTP and nothing else. They
  never touch the `DbContext`.
- **Services** (`api/Klowee.Api/Services/`) hold the business rules and are the
  only layer that talks to the `DbContext`. They throw typed errors that become
  RFC 7807 `ProblemDetails` responses. See
  `docs/decisions/005-service-layer-and-dtos.md`.
- **DbContext** applies the soft-delete query filter and stamps
  `created_at`/`updated_at`/`created_by` on save, so no query or write has to
  remember to.

Errors leave in one shape everywhere: `ProblemDetails`. Validation failures are
400 (automatic, from `[ApiController]` plus data annotations), missing rows 404,
bad credentials 401 with a deliberately vague message, and business-rule
violations 409.

## The admin app

`admin/` is a React 19 + TypeScript SPA built with Vite, routed by React Router
v7 in library mode (`createBrowserRouter`, no route loaders). Redux Toolkit
holds the store; RTK Query owns everything that came from the API.

### How a screen gets its data

```
CategoriesPage                 useListCategoriesQuery()
  component renders       ──►  generated RTK Query hook
  { data, isLoading }     ◄──  cache entry, keyed by endpoint + arguments
                                      │  (miss, or invalidated)
                                      ▼
                               baseQuery  ──►  fetch  ──►  GET /api/menu/categories
                          adds Authorization,             ASP.NET Core
                          handles 401
```

- The component never fetches. It calls a hook and renders whatever the hook
  reports: skeleton rows while `isLoading`, the table once `data` arrives, the
  API's message on `error`. **There is no `useEffect` for data anywhere in the
  app.**
- A cache entry is shared by every component asking for the same endpoint and
  arguments, so two screens showing categories make one request.
- Mutations (`useCreateCategoryMutation`, …) declare `invalidatesTags`. RTK
  Query drops the matching cache entries and refetches the ones still on
  screen — which is why a list refreshes after a create without a reload.
- Errors are read out of the API's `ProblemDetails` by `lib/errors.ts` and shown
  as a toast (`features/toasts/`), so a 409 from the API reads the same on every
  screen.

Folders: `app/` (store, router, typed hooks), `api/` (the one API slice),
`features/` (auth, menu, toasts), `components/ui` and `components/layout`,
`lib/`, `types/api.ts` — which mirrors `api/Klowee.Api/Contracts/` exactly.

### How auth is attached

```
LoginPage  ──►  POST /api/auth/login  ──►  authSlice { token, user, expiresAt }
                                                 │            │
                        listener middleware  ◄───┘            └──►  every request:
                        localStorage['klowee.admin.auth']            prepareHeaders adds
                                                                     Authorization: Bearer …
```

- The token is added by `prepareHeaders` inside the base query, once, for every
  endpoint. No call site handles a header.
- Any 401 on a signed-in request clears the slice and raises a "session expired"
  toast; `RequireAuth` — a layout route in front of everything except `/login` —
  sees the empty token and redirects, remembering where the owner was heading.
- The slice is mirrored to `localStorage` so a refresh keeps the session. The
  XSS trade-off that comes with that is written down in
  `docs/decisions/007-rtk-query-and-token-storage.md`.

## Hosting plan

- **Database:** Supabase (managed PostgreSQL). One project per environment; the dev
  project is `klowee-cafe-dev`.
- **API:** a container-friendly host (e.g. Fly.io, Render, or Railway). The Supabase
  connection string is supplied via an environment variable, not committed.
- **admin/ and client/:** static builds (`npm run build` → `dist/`) served from a
  static/CDN host (e.g. Vercel, Netlify, or Cloudflare Pages), each pointed at the
  API's public URL.

Migrations are applied as a deploy step (`dotnet ef database update`), keeping the
database schema in lockstep with the API code.
