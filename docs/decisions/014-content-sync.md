# 014 — Content moves between environments with a purpose-built tool

## Context

For weeks the owners entered the real menu, packages, events and photos into
the dev project (`klowee-cafe-dev`). Production (`klowee-cafe-prod`, decision
013) went live holding only the two owner accounts. The content has to move
across once now, and later the other way, to refresh dev from production.

## Decision

`tools/ContentSync` is a .NET console tool in `Klowee.sln`. It copies the
eleven content tables and their media from a source project to a destination
project.

- **Tables**, in dependency order: `menu_categories`, `menu_items`, `add_ons`,
  `menu_versions`, `menu_version_items`, `packages`, `package_inclusions`,
  `events`, `event_photos`, `announcements`, `site_settings`. The tool has no
  way to name `users` or `__EFMigrationsHistory`.
- **Rows keep their ids**, so a later sync matches the same rows.
  `created_by` becomes NULL, because the source's user ids mean nothing on the
  other side. Soft-deleted rows are left behind. So are live rows whose parent
  is not being copied, because they would break a foreign key. The dry run
  counts these separately as "orphans".
- **Media.** Every URL into the source bucket (`menu_items.photo_url`,
  `events.cover_photo_url`, `event_photos.url`, the `hero_image_url` and
  `story_image_url` settings) is copied to the same path in the destination
  bucket and rewritten to the destination's public URL. An object already
  there at the same size is skipped. Other URLs (external, empty, null) are
  untouched. A path that is not shaped like one the API generates stops the
  run, so a crafted URL can't reach outside the bucket with the service key.
- **Safety.**
  - All six `SYNC_*` variables are required.
  - Each side's database must belong to the same project as its Supabase URL.
    The project comes from the pooler username (`postgres.<ref>`) or a direct
    host (`db.<ref>.supabase.co`), because both projects share one pooler host
    per region.
  - Source and destination must be different projects.
  - Only refs and hosts are printed.
  - The default is a dry run on read-only sessions.
  - `--apply` asks for the destination ref to be typed back.
  - Storage is copied first. Then one transaction deletes the destination's
    rows (children first) and inserts the source's (parents first). Any
    failure rolls back the whole transaction.
- **`--tables`** narrows the scope, and pulls in the tables that depend on the
  ones named. Replacing `menu_items` alone would either break
  `menu_version_items` (restrict keys) or silently empty it (cascade keys).

## Why not `pg_dump`

`pg_dump --data-only` of the content tables, piped to `psql`, would move the
rows. It would not:

- **move the photos.** They live in Supabase Storage, not Postgres.
- **rewrite the URLs.** Every copied photo URL would still point at the dev
  project, which pauses after a week idle. Production pages would break the
  day dev went to sleep.
- **leave out soft-deleted rows, clear `created_by`, or keep `users` out.**
  That's three separate filters a hand-run dump has to get right every time.
- **protect the owners.** A dump restored the wrong way round silently
  overwrites production. The tool's dry run, ref checks and typed confirmation
  exist so that the person running it at 11pm can't do that.

It also needs `pg_dump` and `psql` installed at the right major version, which
this machine doesn't have. The tool needs only the .NET SDK the repo already
uses, and runs the same way in either direction.

## Consequences

- One-off today, reusable for a prod → dev refresh: swap which side is
  `SOURCE` and which is `DEST` (README, "Copying content between
  environments").
- Files are copied byte for byte. A photo uploaded before handover 07's
  processing arrives as the original JPEG, and re-uploading it in the admin
  turns it into WebP.
- Objects in the destination bucket that no row references any more (the
  prod test hero image, for one) stay there, like every orphan in decision 009.
- The destination's content is replaced, not merged. Anything the owners
  enter only in the destination is lost on the next apply in that direction.
  The dry run's "Dest now" column shows how much that is.
