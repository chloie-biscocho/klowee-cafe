# 007 — RTK Query owns server data; the token lives in localStorage

## Context

The admin app needs two things from a state library: cached server data (menus,
items, categories, add-ons) and one piece of genuine client state (who is signed
in). These are not the same problem, and the usual mistake is to treat them as
one — fetching in an effect, copying the response into a slice, and then
maintaining that copy by hand for the rest of the app's life.

## Decision

### Server data belongs to RTK Query, never to a slice

One API slice (`admin/src/api/baseApi.ts`), with feature endpoints added through
`injectEndpoints` in `authApi.ts` and `menuApi.ts`, so there is a single cache
and a single base query rather than one per feature.

- **Components ask for data with a generated hook** — `useListVersionsQuery()` —
  and get `{ data, isLoading, isFetching, error }`. There is no `useEffect` that
  fetches anything anywhere in the app, and no reducer that stores a list.
- **Every endpoint declares its tags.** Queries `providesTags`, mutations
  `invalidatesTags`. Lists provide `{ type, id: 'LIST' }` plus one tag per row,
  so a create invalidates the list while an update can invalidate one row.
  Cross-resource edges are declared too: renaming a category restates every
  menu item and every version detail, because both carry `categoryName`.
- **That is why no screen reloads itself.** A mutation invalidates; RTK Query
  refetches what is mounted. No manual refetch calls, no cache written by hand.
- **Local state is for UI only** — which modal is open, the value in an input
  before submit, the unsaved item list in the version editor.

The version editor is the one place where server data is edited before being
sent back. It keeps `draft: EditorItem[] | null`: `null` means "no local edits"
and the table renders straight from the cache; the first change takes a copy;
a successful save sets it back to `null` so the refreshed server data becomes
the truth again. The alternative — seeding local state from the query in an
effect — is how the two silently drift apart.

### The token lives in one slice and is attached in one place

`authSlice` holds `{ token, user, expiresAt }`. The token is attached to
outgoing requests by `prepareHeaders` in the base query, so a new endpoint is
authenticated because it goes through that base query, not because someone
remembered a header. One 401 handler sits in the same file: it clears the slice
and raises a toast, and `RequireAuth` redirects on the next render.

A 401 raised *while signed out* is a wrong password, not an expired session, so
it passes through untouched to the login form.

### Persistence: localStorage, and the XSS trade-off

The slice is mirrored into `localStorage` under one key, `klowee.admin.auth`, by
a listener middleware on `loggedIn`/`loggedOut`, and read back at startup —
discarding anything malformed or already past `expiresAt`.

**The trade-off, stated plainly: any JavaScript running on this origin can read
`localStorage`. If the admin app ever executes attacker-controlled script, the
token is stolen, and it stays valid for its remaining eight hours.** An
`HttpOnly` cookie would be out of reach of script, which is strictly better
against XSS — but it costs a same-site/CORS story across two origins, CSRF
protection on every mutation, and API changes to issue and clear the cookie.

We accept `localStorage` for now because:

- the token already expires in 8 hours and there are no refresh tokens
  (decision 004), so a stolen token has a short life;
- the app is two accounts behind a login, with no user-generated HTML rendered
  anywhere — React escapes by default and nothing uses `dangerouslySetInnerHTML`;
- there are no third-party scripts on the admin origin.

If any of those change — an embedded widget, a rich-text field, more accounts —
the answer is an `HttpOnly`, `Secure`, `SameSite` cookie plus CSRF tokens, not a
different browser storage API. `sessionStorage` and in-memory storage are not
that answer: they have exactly the same XSS exposure and only cost usability.

## Consequences

- One way to read server data, one way to change it, one place that refreshes.
- Endpoints are boring to add: query, tags, hook.
- A refresh keeps the owner signed in; logout and any 401 clear the session.
- Trade-off: RTK Query's cache is keyed by endpoint *and arguments*, so
  `listItems({ includeArchived: true })` and `listItems({ includeArchived: false })`
  are two cache entries. Both carry the same tags, so both refresh together.
- Trade-off: the token is readable by script on this origin. Revisit if the
  threat model changes; see above.
