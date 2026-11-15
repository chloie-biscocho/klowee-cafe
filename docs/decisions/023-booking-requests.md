# 023 — Booking requests: a form that sends a request, not a booking

## Context

The homepage's Book section offered only "Email us", "Message on Instagram"
and "Facebook". The owners wanted visitors to send their event details from
the site and to find those requests in the admin. They reply by phone or
chat and decide themselves: the cart's diary, the price and the deposit are
agreed in conversation, not on the website.

## Decision

### A request is not a booking

A visitor's form creates a **booking request** (`booking_requests`): who,
how to reach them, the date, the venue, guests, a package and a message. It
confirms nothing and takes no payment. The owners move it through four
statuses by hand: **New** (nobody has answered), **Contacted** (talking it
over), **Confirmed** (it's happening), **Declined** (it isn't). Any status
can go back to any other. A status change records who and when; saving
notes alone doesn't.

The page never tells the visitor more than "Request sent. Klowee will get
back to you." The API answers `{"received": true}` and never echoes the
stored row, so nothing about a request can be read back from the public side.

A confirmed request does not create an event. **Create event** on a request
opens the normal New event form with the name, venue and date filled in;
saving it is the ordinary event flow, and nothing links the two afterwards.
An event has its own life (photos, publishing, a public page); a request is
a conversation note. Linking them would mean keeping two records in step for
no gain at a few requests a month.

### No account, no captcha, no email

- **No account:** a visitor sends one request and waits for a reply. A login
  would stop most of them, for nothing they need to come back to.
- **No captcha:** a captcha costs every real visitor a puzzle, and the
  free ones send each visitor's browser to a third party. Instead:
  - a **honeypot**: a field named `website`, off screen, out of the tab
    order and hidden from screen readers. People leave it empty; form-filling
    bots fill it. A filled one gets the same 201 as anyone, and nothing is
    stored, so a bot learns nothing;
  - a **rate limit per client IP**: 3 an hour and 10 a day, both at once,
    on the real address from `CF-Connecting-IP` as for login and the tab
    page. They are two separate limiters (the hour in the endpoint's
    policy, the day in the global limiter), not one chained limiter: a
    chained one reports itself idle when its hour refills, ASP.NET then
    discards it, and the day count would start over (a test with the
    windows shrunk to seconds caught exactly that). Every attempt counts, refused ones too. A
    limited visitor reads one sentence and is pointed at Instagram or
    Facebook.
  If spam ever gets through both, the next step is a privacy-respecting
  challenge such as Cloudflare Turnstile, which the API's IP handling
  already sits behind.
- **No email, SMS or push:** sending needs a provider, a domain set up for
  mail and a secret to keep. The owners already open the admin daily, and
  the Dashboard shows a "Booking requests" panel and counts New requests in
  the sidebar badge.

### What is stored, and who can see it

Stored: name (100), contact (200, free text: a number, a Messenger name,
an Instagram handle or an email, checked for length only), event date
(today in Manila or later, at most two years ahead), venue (200), guests
(optional, 1 to 5000), package (optional, an active one, its **name copied**
at the time so a rename or delete later doesn't change the request),
message (optional, 1000), status, owner notes (1000), and who changed the
status and when.

- **Only the owners can read requests** (JWT, decision 004); a test proves
  every owner endpoint answers 401 without a token.
- **Nothing personal reaches a log.** The service logs the request's id and
  the outcome (stored, refused, discarded); HTTP logging records the path
  only. A test captures every log line while requests are sent, refused,
  read and edited, and fails if a name, contact, venue, message or note
  appears.
- **Never cached:** every response on `/api/public/bookings`, errors and 429s
  included, is `no-store` and `noindex`.
- **The switch:** the setting `booking_form_enabled` turns the form off
  (exactly "false" means off; missing or anything else means on, so the form
  works before anyone opens Settings). Off, the API refuses with 409 and the
  site shows the Book section exactly as it was. `booking_form_note` is an
  optional line above the form. Both go out in `/api/public/home`.

### How the owners remove a request

**Delete** on a request, after a confirm. It is a soft delete
(`deleted_at`, decision 003): the request disappears from Bookings, the
Dashboard and the count, and stays in the database. An event created from
it is unaffected. Removing it for good (a visitor asking for their details
to be erased) is a manual database delete for now; at a few requests a
month, a purge job isn't worth building yet.

## Consequences

- The public site makes its third kind of write, still with plain `fetch`
  (decision 011 as amended by 17): one POST whose answer is "received",
  nothing to cache or refetch.
- The form is its own lazy chunk (4.5 kB) and its height is reserved, so it
  neither delays the first paint nor moves the page when it arrives.
- Spam control is two cheap layers, not a guarantee. The owners will see
  any spam that gets through in Bookings and can delete it.
