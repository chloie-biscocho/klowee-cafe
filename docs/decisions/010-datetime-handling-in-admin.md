# 010 — Dates stay dates; date-times are entered and shown in Manila time

## Context

The admin app has two kinds of time on the site screens.

- **Event dates** (`startsOn`, `endsOn`) and menu `effectiveFrom` are C#
  `DateOnly`: a day on a calendar, with no time and no zone. "May 21" is May 21
  in Manila and everywhere else.
- **Announcement windows** (`activeFrom`, `activeUntil`) are `DateTimeOffset`:
  instants. The API stores them as UTC (handover 04 converts on write, because
  Npgsql rejects a non-zero offset on `timestamptz`).

The owners think in Manila wall-clock time: "the promo starts at 8 PM on the
3rd". The browser's `<input type="datetime-local">` gives back exactly that —
`"2026-10-03T20:00"` — **with no zone at all**, and `new Date("2026-10-03T20:00")`
reads it in *the laptop's* zone. An owner travelling, or a laptop left on UTC,
would silently schedule the ticker eight hours off.

## Decision

**Plain dates are never converted.** Event dates use `<input type="date">` and
travel as `"yyyy-MM-dd"` strings end to end. They are compared as strings
(ISO dates order correctly), and formatted by `lib/format.ts` with
`timeZone: 'UTC'` so no browser can shift the day. "Today", where a screen needs
it (the next-event preview, form defaults), is `todayInManila()` — the same
business date the API's `IClock` uses (decision 006), not the laptop's date.

**Date-times are Manila wall-clock in the form, instants on the wire**, and the
conversion lives only in `admin/src/lib/dates.ts`:

- `manilaLocalToIso("2026-10-03T20:00")` → `"2026-10-03T20:00:00+08:00"`. The
  input's value is read *as Manila time* by appending the offset; the browser's
  own zone is never consulted. The API turns it into `12:00Z`.
- `isoToManilaLocal("2026-10-03T12:00:00+00:00")` → `"2026-10-03T20:00"`, to
  fill the input when editing.
- `formatManilaDateTime(iso)` uses `Intl.DateTimeFormat` with
  `timeZone: 'Asia/Manila'` for the table: "Oct 3, 2026, 8:00 PM".

The form keeps the raw `datetime-local` strings in react-hook-form and the zod
schema; conversion happens once, in `toAnnouncementRequest`, on submit. Window
validation ("until after from") compares those strings directly, since both are
the same zone and the same shape.

**A fixed `+08:00`, not a time-zone library.** The Philippines has had no
daylight saving since 1978, so Manila's offset is a constant and appending it
is exact. Display still goes through `Intl` with the named zone, which costs
nothing. If the business ever moves to a zone with DST, `manilaLocalToIso` is
the one function that must learn about it (and `Temporal`, once it is broadly
available, is the answer rather than a package).

**"Live now" needs no zone.** It compares instants — `activeFrom <= now <
activeUntil` — and an instant is the same moment everywhere, so the badge is
correct on any laptop.

## Consequences

- An owner sees and types Manila time on every machine, whatever its clock
  zone. `lib/dates.test.ts` runs in any `TZ` and covers an evening time, a
  Manila-morning time that is the previous day in UTC, and New Year's midnight;
  the live smoke confirmed 8:00 PM saved as `12:00Z` and reloaded as 8:00 PM.
- One file to read when a time looks wrong.
- Trade-off: the screens say "Manila time" in hints rather than showing the
  owner's local time. Correct for two owners in one city; revisit if anyone
  edits from abroad and finds it confusing.
- Trade-off: `datetime-local` has minute precision, so seconds are always `:00`.
