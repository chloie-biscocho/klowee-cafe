# 011 — The public site fetches with `useEffect`; the admin app may not

## Context

Decision 007 bans `useEffect` for data in the admin app and puts every server
read behind RTK Query. The public site (`client/`) is also React, also talks to
the same API, and does the opposite: one `fetch` in a `useEffect`. That looks
like inconsistency, so it is written down.

## Decision

`client/src/api/useHomeData.ts` makes the page's one request:

- `useEffect` + `fetch` of `GET /api/public/home`, returning
  `{ status: 'loading' | 'error' | 'ready', data }` and a `retry()`.
- Each run owns an `AbortController`; the cleanup aborts it. A superseded
  request — React StrictMode's double mount in development, a retry while the
  first is in flight, an unmount — is cancelled and can never set state late or
  out of order. An aborted request is not reported as an error.
- `retry()` bumps a counter in the effect's dependencies, so retrying is "run the
  effect again", with the same abort guarantees.
- The result is passed down as props. No context, no store.

## Why the two apps differ

What RTK Query buys the admin app is everything that happens *after* the first
read:

- **Mutations and invalidation.** The admin screens change data constantly, and
  every change must refresh every screen showing it. Tags do that declaratively.
  The public site has no mutations at all.
- **A shared cache across screens.** The admin app has a dozen routes reading
  overlapping lists. The public site has one page and one request.
- **Avoiding drift between copies.** The admin's ban exists because effects that
  copy server data into state go stale the moment another screen writes. Here
  nothing writes, and the copy *is* the page.

For one read-only GET, RTK Query would add a store, a provider, an API slice and
Redux Toolkit plus react-redux in the bundle of a marketing page whose
performance budget matters, to solve problems this page does not have. The effect is twenty lines, does the
three things that make an effect correct — abort on cleanup, distinguish the
three states, ignore aborted requests — and is tested (`App.test.tsx` covers the
error state and the retry).

## When to revisit

If the public site gains a second request that several components share (a
paginated gallery, a menu filter), live updates, or any write — the booking form
in phase 2 — switch it to RTK Query or React Query rather than growing a second
hand-rolled cache. The rule is the same in both apps: the moment server data
needs invalidating, it belongs to a data library, not to an effect.

## Consequences

- The client ships no Redux; its runtime is React and ReactDOM only.
- Freshness comes from the API's `Cache-Control: max-age=60` and a page reload,
  which is the right granularity for marketing content.
- Trade-off: a visitor's open tab does not update itself when the owners edit
  content. Acceptable for this page; it would not be for the admin app.
