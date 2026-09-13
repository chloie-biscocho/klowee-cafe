# 006 — "Today" is a business date, not a UTC date

## Context

`GET /api/menu/versions/current` picks the published version for a context whose
`effective_from` is on or before today. Handover 02 resolved "today" as
`DateOnly.FromDateTime(DateTime.UtcNow)`.

Klowee Cafe operates in Cagayan de Oro, which is UTC+8 year-round (the
Philippines has no daylight saving). So between local midnight and 08:00 the UTC
date is still yesterday: a menu the owners set to start "today" would not be
returned by the public site for the first eight hours of the day — exactly the
hours a coffee cart cares about. Storing the dates differently would not help;
the dates are already correct. The bug is in what "now" means.

## Decision

The business's time zone is configuration, and "today" is a service.

- **`App:TimeZone`** in `appsettings.json`, value `Asia/Manila`. It is not a
  secret, so it is committed rather than hidden in user-secrets. Bound to
  `Common/AppOptions.cs`.
- **`IClock`** (`Services/IClock.cs`) exposes a single member, `Today`, as a
  `DateOnly`. `SystemClock` resolves it by converting `TimeProvider.GetUtcNow()`
  into the configured zone with `TimeZoneInfo.FindSystemTimeZoneById`.
  Registered as a singleton: the zone is looked up once, in the constructor, so
  a typo in the id fails at startup rather than on the first request.
- **Nothing else reads the date.** `MenuVersionService.GetCurrentAsync` takes
  `IClock` instead of calling `DateTime.UtcNow`. Future date-sensitive rules
  (event listings, announcement windows) take the same dependency.
- **Timestamps stay UTC.** This decision is only about *calendar dates* the
  business reasons in. `created_at`, `updated_at`, `deleted_at` and token expiry
  remain `timestamptz` in UTC — the correct storage for an instant.

## Consequences

- A menu effective on the 16th is live from local midnight on the 16th.
- Tests can fix the date. `Klowee.Api.Tests/FakeClock.cs` supplies `IClock` to
  the test host, so "current menu" assertions use fixed dates instead of drifting
  with the calendar or the build machine's zone. `FixedTimeProvider` pins the
  real `SystemClock` to one UTC instant, which is how
  `GetCurrent_UsesTheBusinessTimeZone_WhenUtcIsStillOnYesterday` proves the
  conversion rather than assuming it.
- Trade-off: `Asia/Manila` must exist on the host. On Linux that means the tzdata
  package; .NET also accepts IANA ids on Windows. The failure is loud and at
  startup.
- Trade-off: one business, one zone. If Klowee ever runs a cart in another zone,
  the zone belongs on the thing being scheduled, not on the app.
