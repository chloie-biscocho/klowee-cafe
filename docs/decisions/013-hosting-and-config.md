# 013 — Hosting on free tiers, configured from the environment

## Context

Three apps run locally against the `klowee-cafe-dev` Supabase project. They now
need public URLs, a production database the dev work can't touch, and a way to
ship changes that doesn't depend on someone remembering the steps. The budget
is zero.

## Decision

| Piece | Where | Plan |
|---|---|---|
| API | Render web service, built from `api/Dockerfile` | Free |
| Admin, client | Vercel, one project each, built from the repo | Hobby |
| Database, media | Supabase project `klowee-cafe-prod` | Free |
| Tests, migrations, deploy trigger | GitHub Actions | Free |

### Configuration

- **Every secret and environment-specific value is an environment variable on
  the host**, named `Section__Key` (`ConnectionStrings__Default`, `Jwt__Key`,
  `Supabase__Url`, `Supabase__ServiceRoleKey`, `App__AllowedOrigins`,
  `Seed__Owners__0__Email`…). ASP.NET Core maps `__` to the `:` used in
  `appsettings.json` and user-secrets, so the code reads one key either way.
- **Checked together at startup** (`Common/RequiredConfiguration.cs`). A missing
  key stops the process with every missing name in one message, written as the
  environment variable to set. Production also requires `App__AllowedOrigins`.
- **Committed config holds no secrets**: `appsettings.json` (time zone, log
  levels) and `appsettings.Production.json` (quieter EF Core logs).

### Startup

`Common/StartupPlan.cs` decides, per environment, what happens to the database
as the API starts, and `HostingTests` holds it:

| | Migrate | Seed owners | Seed sample data |
|---|---|---|---|
| Development | yes | yes | yes |
| Production | **no** | yes | no |
| Testing | no | no | no |

- **Production never migrates on startup.** The deploy workflow runs an EF Core
  migrations bundle against the production database before it triggers Render.
  A failed migration then stops the deploy rather than crash-looping a live
  service, and two instances can never race to migrate.
- **The owners are the only Production seed.** `DbSeeder.SeedOwnersAsync` reads
  `Seed__Owners__{0,1}__{Email,Password,DisplayName}`. It is idempotent by
  email and never changes an existing owner, so once both owners exist, the
  password variables can be deleted from Render. A seeding failure (a paused
  database, say) is logged, not fatal: the API still starts and `/api/health`
  reports `unreachable`.

### Running behind Render

- **Port.** Render sets `PORT`. The container's command expands it at start:
  `ASPNETCORE_URLS=http://+:${PORT}`. The default is 8080.
- **Proxy headers.** Render terminates TLS and forwards plain HTTP with
  `X-Forwarded-For` and `X-Forwarded-Proto`. `UseForwardedHeaders` runs first
  and trusts any forwarder, because Render doesn't publish its proxy
  addresses. The request scheme then reads `https`, and the rate limiter sees
  the visitor's IP rather than the proxy's.
- **No HTTPS redirection in the container.** Render's edge already redirects
  HTTP to HTTPS, and inside the container everything is HTTP, so the middleware
  could only misfire (on the health check, for instance). It stays on in
  Development.
- **CORS** allows exactly `App__AllowedOrigins`: the two Vercel production URLs,
  comma-separated. Vercel preview deployments have other URLs and are refused.
  That's acceptable while the owners only use production.
- **Image.** Multi-stage: the .NET 10 SDK restores and publishes
  `-c Release -r linux-x64`, and the `aspnet:10.0` runtime image runs it as the
  non-root `app` user. curl is added only for `HEALTHCHECK`. Render ignores
  Docker's `HEALTHCHECK` and uses its own setting, which points at
  `/api/health` too.

### Operations

- **Logs.** Outside Development, the console logger writes one JSON object per
  line (UTC timestamps), which Render's log viewer keeps whole and searchable.
  Each request gets one Information line (method, path, status, duration) from
  `HttpLoggingMiddleware`, except `/api/health`, which Render polls. No headers
  are logged, so tokens can't leak. EF Core's per-query logs are Warning and up
  in Production.
- **Login rate limit.** `POST /api/auth/login` allows 5 attempts per minute per
  IP (fixed window, no queue), then returns 429. Nothing else is limited. With
  two owners, a sixth attempt in a minute is either a typo spree or a guesser.
- **Swagger** is mapped in Development only. `/api/health` stays anonymous.

### Database connection

Render's free services reach the internet over IPv4 only, and Supabase's
direct connection (`db.<ref>.supabase.co`) is IPv6-only, so both Render and
GitHub Actions use the **pooler** connection string. The session pooler (port
5432) behaves like a direct connection. The transaction pooler (port 6543)
also works with this API's EF Core usage, but migrations are safer on the
session pooler.

## Why these hosts

- **Render** runs a Dockerfile as-is on the free tier, so the API ships with
  exactly the runtime and native libraries (Magick.NET) it was tested with. The
  cost is a cold start after 15 minutes idle (see Consequences).
- **Vercel** builds Vite apps with zero config, serves them from a CDN and
  previews every branch. Two projects, because each app has its own root
  directory and build.
- **Supabase** is already the dev platform. A second project keeps production
  data out of reach of `dotnet run` and of migrations still being written.

## Consequences

- **Cold starts.** The free Render service sleeps after about 15 minutes without
  traffic. The next request waits roughly 30–60 seconds while the container
  starts. The public site shows its loading state meanwhile, and the admin's
  first request after a quiet spell is slow.
- **Supabase free pauses** a project after a week of inactivity, and it has to
  be resumed from the dashboard. Regular site traffic prevents it.
- **Deploys are serial and deliberate**: push → CI → migrate → deploy hook →
  Render builds. Render's own auto-deploy is off, so an image never goes live
  ahead of its migration.
- **Two copies of each secret.** The deploy workflow needs the database
  connection (GitHub secret), and the running API needs everything (Render
  environment). Rotating a value means updating both.
