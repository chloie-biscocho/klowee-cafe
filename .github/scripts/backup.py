#!/usr/bin/env python3
"""Helpers for .github/workflows/backup.yml (standard library only).

  pgenv            PROD_DB_CONNECTION (Npgsql key=value form) -> a 0600
                   ~/.pgpass plus PGHOST/PGPORT/PGDATABASE/PGUSER/PGSSLMODE in
                   $GITHUB_ENV, so pg_dump never sees the password on a
                   command line or in the environment.
  upload FILE KEY  FILE -> private bucket "backups" at KEY (overwrites a same-day rerun).
  prune DAYS       delete db/ objects whose name is dated more than DAYS ago.

Secrets come from the environment and are never printed. Decision 015.
"""
import json
import os
import re
import sys
import urllib.error
import urllib.request
from datetime import date, timedelta
from pathlib import Path

BUCKET = "backups"
NAME = re.compile(r"klowee-prod-(\d{4})-(\d{2})-(\d{2})\.dump\.age$")


def parse_npgsql(connection: str) -> dict:
    """'Host=a;Port=5432;Password="x;y"' -> {'host': 'a', 'port': '5432', 'password': 'x;y'}."""
    parts, key, value, quote, in_value = {}, "", "", None, False
    for ch in connection + ";":
        if in_value:
            if quote:
                if ch == quote:
                    quote = None
                else:
                    value += ch
            elif ch in "'\"" and not value.strip():
                quote = ch
            elif ch == ";":
                parts[key.strip().lower()] = value.strip()
                key, value, in_value = "", "", False
            else:
                value += ch
        elif ch == "=":
            in_value = True
        elif ch == ";":
            key = ""
        else:
            key += ch
    aliases = {
        "host": ["host", "server"], "port": ["port"], "database": ["database", "db"],
        "user": ["username", "user id", "userid", "user"], "password": ["password", "pwd"],
        "sslmode": ["ssl mode", "sslmode"],
    }
    found = {name: next((parts[a] for a in keys if a in parts), "") for name, keys in aliases.items()}
    missing = [n for n in ("host", "database", "user", "password") if not found[n]]
    if missing:
        sys.exit(f"PROD_DB_CONNECTION is missing: {', '.join(missing)} (expected the .NET key=value form)")
    return found


def pgenv() -> None:
    c = parse_npgsql(os.environ["PROD_DB_CONNECTION"])
    print(f"::add-mask::{c['password']}")
    port = c["port"] or "5432"
    pgpass = Path.home() / ".pgpass"
    escape = lambda s: s.replace("\\", "\\\\").replace(":", "\\:")
    pgpass.write_text(":".join(escape(x) for x in (c["host"], port, c["database"], c["user"], c["password"])) + "\n")
    pgpass.chmod(0o600)
    # Supabase requires TLS; "Require" in Npgsql maps to libpq's require.
    sslmode = (c["sslmode"] or "require").lower()
    with open(os.environ["GITHUB_ENV"], "a") as env:
        env.write(f"PGHOST={c['host']}\nPGPORT={port}\nPGDATABASE={c['database']}\nPGUSER={c['user']}\n")
        env.write(f"PGSSLMODE={sslmode if sslmode in ('require', 'verify-ca', 'verify-full') else 'require'}\n")
        env.write(f"PGPASSFILE={pgpass}\n")
    print(f"Database host {c['host']}, port {port}, database {c['database']}")


def request(method: str, path: str, body=None, data: bytes | None = None, content_type="application/json"):
    # Secrets pasted into GitHub often carry a trailing newline; a header
    # value cannot contain one, and a key or URL never contains whitespace.
    base = os.environ["PROD_SUPABASE_URL"].strip().rstrip("/")
    for suffix in ("/rest/v1", "/storage/v1", "/auth/v1"):
        if base.endswith(suffix):
            base = base[: -len(suffix)]
    key = os.environ["PROD_SUPABASE_SERVICE_KEY"].strip()
    payload = data if data is not None else (json.dumps(body).encode() if body is not None else None)
    req = urllib.request.Request(f"{base}/storage/v1/{path}", data=payload, method=method, headers={
        "Authorization": f"Bearer {key}", "apikey": key, "Content-Type": content_type, "x-upsert": "true"})
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            text = resp.read().decode() or "null"
            return json.loads(text)
    except urllib.error.HTTPError as e:
        sys.exit(f"Storage {method} {path.split('?')[0]} failed: HTTP {e.code} {e.read().decode()[:300]}")


def upload(file: str, key: str) -> None:
    data = Path(file).read_bytes()
    request("POST", f"object/{BUCKET}/{key}", data=data, content_type="application/octet-stream")
    print(f"Uploaded {len(data)} bytes to {BUCKET}/{key}")


def list_folder(prefix: str) -> list:
    """Supabase lists one level at a time: entries without an id are folders."""
    found, offset = [], 0
    while True:
        page = request("POST", f"object/list/{BUCKET}", {"prefix": prefix, "limit": 100, "offset": offset})
        for entry in page:
            path = f"{prefix}/{entry['name']}" if prefix else entry["name"]
            found += list_folder(path) if entry.get("id") is None else [path]
        if len(page) < 100:
            return found
        offset += 100


def expired(paths: list, today: date, days: int) -> list:
    cutoff = today - timedelta(days=days)
    old = []
    for path in paths:
        m = NAME.search(path)
        if m and date(int(m[1]), int(m[2]), int(m[3])) < cutoff:
            old.append(path)
    return old


def prune(days: int) -> None:
    paths = list_folder("db")
    old = expired(paths, date.today(), days)
    if old:
        request("DELETE", f"object/{BUCKET}", {"prefixes": old})
    print(f"{len(paths)} backups in {BUCKET}/db, {len(old)} older than {days} days deleted")
    for path in old:
        print(f"  deleted {path}")


if __name__ == "__main__":
    command = sys.argv[1] if len(sys.argv) > 1 else ""
    if command == "pgenv":
        pgenv()
    elif command == "upload" and len(sys.argv) == 4:
        upload(sys.argv[2], sys.argv[3])
    elif command == "prune" and len(sys.argv) == 3:
        prune(int(sys.argv[2]))
    else:
        sys.exit(__doc__)
