# 001 — Single monorepo for API, admin, and client

## Context

Klowee Cafe needs three apps: a backend API, a co-owner admin dashboard, and a public
marketing site. They share concepts (menu, packages, events) and will evolve together.
The team is one Flutter developer learning fullstack, plus a co-owner. Coordinating
three separate repositories (versions, issues, cross-cutting changes) is overhead a
small team does not need yet.

## Decision

Keep all three apps in one Git repository (`klowee-cafe`) with top-level folders
`api/`, `admin/`, and `client/`, plus shared `docs/`. Each app keeps its own build
tooling and dependencies; there is no shared build system beyond the folder layout.

## Consequences

- One place to clone, one issue tracker, atomic commits that span API + frontend.
- Simpler onboarding and a single source of documentation and decision records.
- Trade-off: CI must scope builds per-folder, and the repo mixes .NET and Node
  toolchains. Acceptable at this size.
- If an app later needs independent release cadence or ownership, it can be split out;
  the folder boundary makes that low-cost.
