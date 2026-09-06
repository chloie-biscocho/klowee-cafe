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

Authentication is **not** built yet. When added, only the `admin/` app will require a
signed-in Owner; `client/` will stay anonymous and read-only.

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
