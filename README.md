# Klowee Cafe

Monorepo for **Klowee Cafe**, a small pop-up coffee cart business in Cagayan de Oro,
Philippines, run by two co-owners.

## Apps

| Folder    | Stack                                             | Purpose                          |
| --------- | ------------------------------------------------- | -------------------------------- |
| `api/`    | ASP.NET Core Web API, EF Core, PostgreSQL         | Backend API + data              |
| `admin/`  | React + Vite + TypeScript + Redux Toolkit         | Co-owner-only admin dashboard    |
| `client/` | React + Vite + TypeScript                         | Public marketing website         |

The database is PostgreSQL, hosted on **Supabase**. Schema is managed exclusively
through EF Core migrations (never the Supabase table editor).

See [`docs/architecture.md`](docs/architecture.md) and
[`docs/decisions/`](docs/decisions) for the design and the rationale behind key choices.

## Prerequisites

- .NET SDK 10 (LTS)
- Node.js 20+ (LTS) and npm
- A Supabase project (development database)
- `psql` (optional, for ad-hoc inspection)

## Running it locally

Three terminals: the API first, then either front end. The API must be running
before the admin app is useful — every admin screen reads from it.

### 1. API — http://localhost:5181

```bash
cd api

# One-time: secrets, which are never committed.
dotnet user-secrets set "ConnectionStrings:Default" "<supabase-connection-string>" --project Klowee.Api
dotnet user-secrets set "Jwt:Key"                   "<32+ random characters>"      --project Klowee.Api
dotnet user-secrets set "Seed:Owners:0:Email"       "<owner email>"                --project Klowee.Api
dotnet user-secrets set "Seed:Owners:0:Password"    "<owner password>"             --project Klowee.Api
dotnet user-secrets set "Seed:Owners:0:DisplayName" "<owner name>"                 --project Klowee.Api
# ...repeat with Seed:Owners:1:* for the second co-owner.

# Media uploads go to a public Supabase Storage bucket named "media":
dotnet user-secrets set "Supabase:Url"            "https://<project-ref>.supabase.co" --project Klowee.Api
dotnet user-secrets set "Supabase:ServiceRoleKey" "<service role key>"                --project Klowee.Api

dotnet ef database update --project Klowee.Api    # Development also does this on startup
ASPNETCORE_ENVIRONMENT=Development dotnet run --project Klowee.Api --launch-profile http
```

- Health check: <http://localhost:5181/api/health>
- Swagger (Development only): <http://localhost:5181/swagger>
- Tests: `dotnet test Klowee.sln`

`appsettings.Development.json` is git-ignored. See
[`api/Klowee.Api/appsettings.Development.example.json`](api/Klowee.Api/appsettings.Development.example.json)
for the expected shape. Non-secret settings such as `App:TimeZone`
(`Asia/Manila`, decision `006`) live in `appsettings.json` and are committed.

> If the API fails to start with `tenant/user … not found`, the Supabase dev
> project is paused. Open it in the Supabase dashboard to resume it.

The API refuses to start in Development without the Supabase storage settings.
`Supabase:Url` is the **project** URL — the dashboard also shows the REST and
Storage endpoints on the same page, and either of those pasted here is
normalised back to the project root rather than failing oddly later.

### 2. Admin — http://localhost:5173

```bash
cd admin
npm install
cp .env.example .env      # VITE_API_URL=http://localhost:5181
npm run dev
```

Sign in at <http://localhost:5173/login> with one of the `Seed:Owners:*`
accounts. The port is pinned to 5173 because the API's development CORS policy
allows exactly that origin.

| Command | What it does |
| ------- | ------------ |
| `npm run dev` | Vite dev server on 5173 |
| `npm run build` | Type-check (`tsc -b`) then production build into `dist/` |
| `npm test` | Vitest + React Testing Library + MSW, once |
| `npm run test:watch` | The same, in watch mode |
| `npm run lint` | oxlint |

### 3. Client — http://localhost:5174

```bash
cd client
npm install
npm run dev
```

Still the untouched Vite scaffold.

## Repository conventions

- Tables and columns use `snake_case` (via `EFCore.NamingConventions`).
- Every table carries audit + soft-delete columns; see decision record `003`.
- Secrets live in `dotnet user-secrets` locally and in the host's environment in
  production. They are never committed.
