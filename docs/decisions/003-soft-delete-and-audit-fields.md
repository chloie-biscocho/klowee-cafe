# 003 — Soft delete and audit fields

## Context

This is a small business run by non-engineers. Accidental deletes should be
recoverable, and we want a basic trail of when rows were created and changed (and by
whom, once auth exists). Hard-deleting rows would also break historical references
(e.g. a menu version item referenced by a past order).

## Decision

Every table inherits a common **BaseEntity** with:

- `id` (uuid, generated), `created_at`, `updated_at`, `created_by` (uuid, nullable
  until auth exists), and `deleted_at` (nullable).

Deletes are **soft**: instead of removing a row, we set `deleted_at`. A single EF Core
**global query filter** (`deleted_at == null`) is applied to every entity, so normal
queries never see soft-deleted rows without each query having to remember to filter.
`created_at`/`updated_at` are maintained centrally in the DbContext's `SaveChanges`.

## Consequences

- Deletes are recoverable; historical foreign-key references stay intact.
- Every query is automatically scoped to live rows; opting in to deleted rows is
  explicit (`IgnoreQueryFilters`).
- Trade-off: uniqueness constraints and queries must account for soft-deleted rows;
  "deleted" data still occupies the table and needs occasional housekeeping.
