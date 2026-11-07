# 015 — Daily encrypted database backups, kept in two places

## Context

The production database is on Supabase's free tier, which keeps no backups and
offers no point-in-time recovery. The owners' menu, packages and events
(handover 09) exist only there. One bad migration, a mistaken bulk delete or
a lost project, and they're gone.

## Decision

`.github/workflows/backup.yml` runs **daily at 18:00 UTC (02:00 Manila)**, and
on demand.

1. It installs the PostgreSQL client matching the server's major version
   (`PG_MAJOR`, 17 today) and **fails if the server reports a different
   major**, so a Supabase upgrade can't silently produce dumps the pinned
   client can't make.
2. It runs `pg_dump --format=custom --no-owner --no-privileges --schema=public`:
   every app table, `users` and `__EFMigrationsHistory` included. Supabase's
   own schemas (`auth`, `storage`, …) belong to the platform and come with any
   new project. The password goes into a `0600` `.pgpass`, never onto a command
   line.
3. It **fails loudly** on a non-zero `pg_dump`, a dump under 10 kB, an archive
   `pg_restore --list` can't read, or one missing data for `users`,
   `__EFMigrationsHistory`, `menu_items`, `events` or `site_settings`. Any
   failed upload also fails the job, and GitHub emails the owner whenever the
   job fails.
4. It encrypts the dump with **`age`** to the public key in
   `BACKUP_AGE_PUBLIC_KEY`, then shreds the plain dump.
5. It keeps the encrypted file in **two places**: a workflow artifact for **30
   days**, and the private bucket `backups` at
   `db/YYYY/MM/klowee-prod-YYYY-MM-DD.dump.age` for **90 days**. Bucket objects
   older than 90 days are deleted at the end of each run.

Restoring is `docs/runbooks/restore-database.md`.

## Why

- **Daily.** The owners edit content a few times a week. A day is the most
  work a restore can lose, and that's re-typing at most. More often would
  only multiply near-identical files. Dumps are tens of kB, so storage costs
  nothing at this size.
- **`age` over GPG.** `age` has one key type, one command to encrypt and one
  to decrypt, with no keyring, trust model or expiry to manage. A recipient is
  a single `age1…` string that fits in a GitHub secret. GPG can do the same,
  but every step has more ways to go wrong for someone doing it for the first
  time, at the worst possible moment.
- **Public-key encryption.** CI holds only the public key, which can encrypt
  but not decrypt. Someone who steals the secrets, the artifacts or the bucket
  gets ciphertext. The private key lives offline with the owners.
- **Two destinations.**
  - *Artifacts* live outside Supabase, so they survive the very event a
    backup exists for: losing the Supabase project or account. They expire
    after 30 days (GitHub caps retention at 90).
  - *The bucket* keeps three months without depending on GitHub. It sits
    inside Supabase, so on its own it would not survive losing the project.
  - Each covers the other's failure.
- **30 and 90 days.** A month covers "we noticed last week"; three months
  covers "the March menu was right, what changed?". Both stay well inside the
  free tiers (Supabase's 1 GB of storage holds years of these dumps).

## Not covered

- **The `media` bucket.** Photos aren't in the dump. A restored database points
  at the old project's photo URLs. If that project is lost, the photos are
  lost too, unless the owners still have the originals. Backing up storage is
  a separate decision.
- **Point-in-time recovery.** The newest backup can be up to a day old.
  Anything finer needs Supabase's paid PITR.
- **Restore drills.** The workflow proves a dump is readable
  (`pg_restore --list`), not that a restore works end to end. A restore
  rehearsed into a scratch project, following the runbook, is the only proof.
