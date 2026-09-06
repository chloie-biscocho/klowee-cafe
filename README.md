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

## Getting started

### API

```bash
cd api
# Provide the Supabase connection string locally (never committed):
dotnet user-secrets set "ConnectionStrings:Default" "<your-supabase-connection-string>" --project Klowee.Api
dotnet ef database update --project Klowee.Api
dotnet run --project Klowee.Api
# Health check:  GET http://localhost:5xxx/api/health
# Swagger (Dev): http://localhost:5xxx/swagger
```

`appsettings.Development.json` is git-ignored. See
[`api/Klowee.Api/appsettings.Development.example.json`](api/Klowee.Api/appsettings.Development.example.json)
for the expected shape.

### Admin & Client

```bash
cd admin   # or: cd client
npm install
npm run dev
```

## Repository conventions

- Tables and columns use `snake_case` (via `EFCore.NamingConventions`).
- Every table carries audit + soft-delete columns; see decision record `003`.
- Secrets live in `dotnet user-secrets` locally and in the host's environment in
  production. They are never committed.
