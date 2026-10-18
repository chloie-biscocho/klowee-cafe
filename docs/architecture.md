# Architecture

Klowee Cafe is a monorepo with three deployable apps plus a shared PostgreSQL database.

## The three apps

- **`api/` — ASP.NET Core Web API (EF Core + PostgreSQL).** The single source of
  truth for data and business rules. Exposes a JSON HTTP API. Owns the database
  schema through EF Core migrations.
- **`admin/` — React + Vite + TypeScript + Redux Toolkit.** A private dashboard the
  two co-owners use to manage the menu, packages, events, announcements and site
  settings. Redux Toolkit holds client state and cached server data.
- **`client/` — React + Vite + TypeScript + Tailwind.** The public one-page site.
  Read-only: one anonymous request to `GET /api/public/home`, no router, no store.

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

Folders: `app/` (store, router, the lazily loaded pages in `pages.tsx`, typed hooks), `api/` (the one API slice plus
one `injectEndpoints` file per domain), `features/` (auth, menu, packages,
events, announcements, settings, toasts), `components/ui` and `components/layout`,
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

### Uploading from the admin app

```
ImageUpload (components/ui)                       uploadsApi.uploadImage
  click → hidden <input type=file>                  FormData { file }
  or drop → dataTransfer.files[0]          ──►      POST /api/uploads?folder=…
        │                                           (browser sets the multipart
  precheck: JPEG/PNG/WebP, ≤ 5 MB                    boundary header itself)
  (courtesy only; inline message)                          │
        │                                                  ▼
  spinner over the drop zone            ◄──  { url, … }  or ProblemDetails 400
        │
  onChange(url) ──► the form field (react-hook-form Controller)
        │
  saved with the entity: item photoUrl, event coverPhotoUrl,
  event photo list, hero/story *_image_url settings
```

- `ImageUpload` takes `value`, `onChange`, `folder` and `label`, so every image
  field in the app is the same component. It never touches the entity: the
  upload only produces a URL, and the form's normal save stores it.
- A failed upload shows the API's message under the drop zone and leaves the
  previous value alone. **Remove** only clears the field (`onChange(null)`); the
  object stays in the bucket — the orphan gap in decision 009.
- The event detail page's "Add photo" is an `ImageUpload` whose value is always
  empty: each upload appends to the unsaved photo list, which `Save photos`
  sends in full to `PUT /api/events/{id}/photos`.
- Times on the announcements screen cross a time zone on the way in and out;
  that conversion lives in `lib/dates.ts` (decision 010).

## Media uploads

Photos go to a public Supabase Storage bucket named `media`, and only the API
ever writes to it (`docs/decisions/009-media-storage.md`).

```
admin (browser)                 api                                Supabase Storage
  multipart POST          ──►   UploadsController  (HTTP only)
  field "file"                       │
  ?folder=menu|events|site           ▼
                                UploadService
                                  validate: folder, content type,
                                    size ≤ 5 MB, magic bytes
                                  process (Magick.NET):
                                    MagickImageInfo: side > 8000px → 400
                                    decode (JPEG/PNG/WebP only), ICC → sRGB
                                    AutoOrient (EXIF rotation into pixels)
                                    fit within 1600×1600, never enlarge
                                    drop EXIF/XMP/IPTC/ICC
                                    encode WebP, quality 82
                                  path: folder/yyyy/MM/{guid}.webp
                                     │
                                     ▼
                                IStorageService    ──►  SupabaseStorageService
                                                        POST /storage/v1/object/media/{path}
                                                        Authorization: service role key
                                                                     │
                                                                     ▼
  { url, path, contentType: image/webp, size }  ◄─────────  public URL, readable by anyone
```

- The **declared content type and the file name are the caller's word**; the
  first bytes are not. `UploadService` reads the signature and makes it agree
  before anything is stored.
- The **path is generated by the server**, never taken from the upload, so there
  are no collisions, no traversal, and no user-supplied names on a public URL.
- **Every stored image is a processed WebP** whatever was uploaded, at most
  1600px on its longest side and free of metadata (location included). `size`
  in the response is the stored size. Objects uploaded before handover 07 are
  the originals; re-uploading replaces them. Decision 012 covers why.
- `IStorageService` is the seam: the rules live in `UploadService`, the transport
  in `SupabaseStorageService`, and tests substitute a fake for the latter.
- The returned `url` is what gets stored on an entity (`events.cover_photo_url`,
  `event_photos.url`, the `*_image_url` settings). Those are plain strings, not
  foreign keys — deleting the row does not delete the object.

## The public endpoint

`GET /api/public/home` is the only endpoint the marketing site calls in phase 1.
It is anonymous and answers with `Cache-Control: public, max-age=60`.

```
client (browser)  ──►  GET /api/public/home  ──►  PublicController  ──►  PublicHomeService
                                                                              │
   composes from the services that already own the rule:                      │
     ISiteSettingService     the known keys, as a map                         │
     IAnnouncementService    GetCurrentAsync   — active, started, unexpired   │
     IEventService           GetNextAsync      — published, Upcoming, not over│
     IPackageService         ListAsync(false)  — active only                  │
                                                                              │
   queries the DbContext directly, where the public shape differs:            ▼
     menu    grouped by category, with each item's description and photo
     events  published, with their photos inline, upcoming before past
```

One payload, one round trip, one place that decides what a visitor may see:
draft menu versions, unavailable items, inactive packages, unpublished events
and expired announcements are all filtered here. `IMenuVersionService
.FindCurrentIdAsync` and `IClock` are still the single source of "which version
is live" and "what day is it in Manila", so the public site and the admin app
cannot disagree.

## The client site

`client/` is the public page: React 19, TypeScript strict, Vite, Tailwind v4,
nothing else at runtime. The visual spec is `docs/design-brief.md`.

```
main.tsx ─► App ─► useHomeData()  ──►  fetch GET /api/public/home   (one request, anonymous)
                     { status: loading | error | ready, data, retry }
                │
                ├─ loading ─► LoadingState   (paper hero skeleton, three blocks)
                ├─ error   ─► ErrorState     (retry → the effect runs again)
                └─ ready   ─► StickerBar · Nav · Hero · Story · Menu ·
                              Packages · Popups · Book · Footer
                              (each a file in src/sections/, each given the payload)
```

- **No router, no store.** Navigation is `#anchor` links to sections; the payload
  is fetched once in `App` and passed down as a prop. Decision 011 covers why
  this differs from the admin app.
- **Every section renders from missing data.** A null setting, a null menu or an
  empty list produces a placeholder or an empty state (`Placeholder`,
  `TentSketch`), never an error; Packages disappears entirely, with its nav link.
- **Design tokens live in `src/index.css`'s `@theme`**, with Tailwind's default
  palette switched off, so a class like `bg-red-500` simply does not exist and
  no hex appears in a component.
- **The wordmark probes for `public/logo.svg`** once per page load and falls back
  to the Fuzzy Bubbles lettering, so the real logo is a file drop, not a change.
- **The browser caches the payload** for up to 60 seconds (the API's
  `Cache-Control`); there is no client-side cache beyond that.

## Hosting and delivery

| Piece | Host | Built from |
|---|---|---|
| API | Render web service (Docker, free) | `api/Dockerfile` |
| admin, client | Vercel, one project each (Hobby) | `admin/`, `client/` |
| Database, media bucket | Supabase `klowee-cafe-prod` (free) | migrations bundle in CI |

Configuration is environment variables on each host, `Section__Key` for the API
(decision 013). The dev project `klowee-cafe-dev` stays the local database.

### From a push to a running API

```
git push ──► CI (ci.yml)            api: build + test   admin / client: lint + test + build
                 │  all green, branch main, event push
                 ▼
             Deploy (deploy.yml)    one at a time (concurrency group)
               migrate              dotnet-ef 10.0.11 → migrations bundle (linux-x64)
                 │                  ./efbundle --connection $PROD_DB_CONNECTION
                 ▼                  (fails here → nothing is deployed)
               deploy-api           POST $RENDER_DEPLOY_HOOK_URL&ref=<that commit>
                 │
                 ▼
             Render                 docker build api/ → start container
                                    → health check GET /api/health → swap traffic

Vercel, in parallel: every push builds admin/ and client/ (previews for branches,
production for main), with VITE_API_URL baked in at build time.
```

- **Migrations never run on API startup in Production** (`Common/StartupPlan.cs`);
  the bundle is the only thing that changes the production schema, and it runs
  before the new image exists.
- **Render's auto-deploy is off**, so the deploy hook is the only trigger, and
  `ref=` makes Render build exactly the commit that was migrated.
- Production startup only ensures the two owner accounts exist.
