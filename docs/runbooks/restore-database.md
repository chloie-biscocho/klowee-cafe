# Runbook: restore the production database from a backup

Use this when production's data is lost or damaged, or to rehearse. The
backups come from `.github/workflows/backup.yml` (decision 015): a
`pg_dump --format=custom` of the `public` schema (every app table, `users`
and `__EFMigrationsHistory` included), encrypted with `age`.

**You need:** the age **private key** file (kept offline by the owners, never
in the repo or in GitHub), a WSL/Linux shell, and 30 minutes.

## 1. Get the backup file

Pick the newest backup from before the damage:

- **Last 30 days:** GitHub → **Actions** → **Backup** → the run → *Artifacts* →
  `klowee-prod-YYYY-MM-DD.dump.age` (it downloads as a zip; unzip it).
- **Last 90 days:** Supabase → `klowee-cafe-prod` → **Storage** → bucket
  `backups` → `db/YYYY/MM/` → the file → **Download**.

## 2. Install the tools (once)

The client's major version must be at least the server's. Supabase runs
PostgreSQL 17 (check `PG_MAJOR` in `backup.yml`).

```bash
sudo install -d /usr/share/postgresql-common/pgdg
sudo curl -fsSL -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc https://www.postgresql.org/media/keys/ACCC4CF8.asc
echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt $(. /etc/os-release; echo $VERSION_CODENAME)-pgdg main" \
  | sudo tee /etc/apt/sources.list.d/pgdg.list
sudo apt-get update && sudo apt-get install -y postgresql-client-17 age
export PATH=/usr/lib/postgresql/17/bin:$PATH
```

## 3. Decrypt and look inside

```bash
age --decrypt --identity ~/klowee-backup.key --output klowee.dump klowee-prod-YYYY-MM-DD.dump.age
pg_restore --list klowee.dump | grep "TABLE DATA"     # the tables a restore will fill
```

`klowee.dump` now holds every owner's password hash. Keep it out of the repo,
and `shred -u klowee.dump` when you're done.

## 4. Create the new database

Supabase → **New project** → name it (e.g. `klowee-cafe-prod-2`) → region
**`eu-west-1`**, next to the Frankfurt API (decision 013) → save the database
password. Then **Connect** → **Session pooler**, and copy two forms of the
connection:

- the **parameters** view (host, port, database, user), for `pg_restore` below;
- **Type: .NET**, with the password filled in, for GitHub and Render in step 6.

If the old project still exists and only its data is damaged, you can restore
into it instead. Drop the damaged tables first, or step 5 will report
"already exists" errors for every one of them.

## 5. Restore

```bash
pg_restore --no-owner --no-privileges --schema=public --verbose \
  --dbname="host=<pooler host> port=5432 dbname=postgres user=postgres.<new-ref> sslmode=require" \
  klowee.dump
```

It asks for the database password. At the end it reports errors, if any. An
error that only says `schema "public" already exists` is harmless; every
fresh project has that schema. Any other error means stop and read it.

Check the row counts against what you expect:

```bash
psql "host=<pooler host> port=5432 dbname=postgres user=postgres.<new-ref> sslmode=require" -c \
  "select (select count(*) from users) users, (select count(*) from menu_items) menu_items,
          (select count(*) from events) events, (select count(*) from site_settings) settings;"
```

## 6. Point production at it

1. GitHub → **Settings → Secrets → Actions** → `PROD_DB_CONNECTION`: the new
   **.NET** connection string.
2. GitHub → **Actions → Deploy → Run workflow** (`main`). Its `migrate` job
   applies any migration newer than the backup; if the backup is current it
   does nothing.
3. Render → `klowee-cafe-api` → **Environment** → `ConnectionStrings__Default`
   = the same .NET string → **Save, rebuild and deploy**.
4. If you moved to a **new project**, also update `Supabase__Url` and
   `Supabase__ServiceRoleKey` on Render, and `PROD_SUPABASE_URL` /
   `PROD_SUPABASE_SERVICE_KEY` in GitHub. Then create a public bucket named
   `media` and a private bucket named `backups` in the new project.

## 7. Check

- `https://klowee-cafe-api.onrender.com/api/health` says `"database":"connected"`.
- An owner can sign in to the admin. Passwords are restored with `users`, so
  the old passwords work.
- The client shows the menu, packages and events.

## What a restore does not bring back

- **Photos.** They live in the `media` storage bucket, which isn't in the
  backup. The restored rows still point at the **old** project's photo URLs.
  If the old project is gone, so are the photos, and they must be uploaded
  again. If it still exists, the photos keep working. To bring them into the
  new project, run `tools/ContentSync` from the old project to the new one
  (README, "Copying content between environments").
- **Anything after the backup.** Backups run daily at 02:00 Manila time, so up
  to a day of edits can be lost.
