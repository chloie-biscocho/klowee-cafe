# 021 — Customer tab links: a private page per customer, reached by an unguessable URL

## Context

Office customers keep asking the owners how much they owe. The owners want
each customer to see their own tab, the unpaid orders behind it and their
loyalty cards, and to be able to pay by GCash or bank transfer and say so.
There are about forty customers, none of whom has an account, and the
owners send everything over Messenger.

## Decision

### Capability links instead of accounts

Each customer can have one link, `https://<site>/tab/{token}`. The token is
32 bytes from `RandomNumberGenerator`, base64url (43 characters, 256 bits),
stored in `customers.tab_token` behind a unique index. Holding the URL *is*
the permission: there is no login, password, email or session.

- The owners create a link from the customer's page (or in bulk for everyone
  with a balance), copy it and send it. **Reset** replaces the token, and the
  old URL stops working at once; the API also has `DELETE …/tab-link`.
- Any token that isn't a live link (malformed, replaced, removed) gets the
  same lookup and the same 404, so a caller learns nothing by guessing.
- Accounts would mean passwords to reset, emails to send and a sign-up nobody
  would finish, for a page that shows a balance. A link fits a page that is
  read now and then and is worth little to anyone else. Decision 004 keeps
  JWT accounts for the owners, whose access is worth protecting properly.

### What the page exposes, and what keeps the token secret

`GET /api/public/tab/{token}` returns only what the customer may see: their
nickname (else name), the tab (orders they pay for, "c/o" ones included,
oldest unpaid first, as item names and amounts), their own loyalty cards
(slots, dates, rewards; no order ids), their last five payments and claims,
and the active ways to pay. Never another customer's name, notes, discount
reasons or internal ids, except claims', clubs' and payment methods' ids,
which the page needs to post. A test asserts this on the raw JSON.

- Every response (errors and 429s too) is `Cache-Control: no-store` and
  `X-Robots-Tag: noindex`; Vercel adds the same headers to `/tab/*`, and the
  page carries `<meta name="robots" content="noindex">`. No analytics.
- **Tokens never reach a log.** The HTTP logging middleware skips
  `/api/public/tab`, the exception handler logs the path through
  `TabLinks.Redact`, and the service logs the customer id instead. A test
  captures every log line the app writes and fails if a token appears.
- Rate limits: reads 60 a minute per client IP; claims 5 an hour and card
  requests 3 a day per token (hashed before it becomes a partition key).

### Claims, not automatic payments

"I've paid" creates a **payment claim** (amount, reference, the method's
channel); it moves no money. The owners check their GCash or bank app and
**Confirm** (the usual settle runs inside one transaction: oldest order first,
stamps through `ReconcileAsync`, payments linked to the claim) or **Reject**
with a reason the customer reads. A claim may not exceed the tab less what is
already pending; confirming refuses (409) if the tab shrank meanwhile, and
the claim leaves Pending with one conditional update so a double confirm
cannot settle twice. The page is therefore eventually consistent: a reported
payment shows as "waiting" until an owner confirms it.

The alternative, trusting the customer's report, would let anyone with a link
mark their tab paid. Verifying a reference automatically needs the bank's
API, which personal GCash and CIMB accounts don't offer.

### Cards by request

A customer without a card sees it dimmed with an **Ask for a card** button,
which creates a `Requested` membership (decision 020, handover 16a) for the
owners to give or decline from the dashboard.

## What a payment gateway would change

A gateway (PayMongo, Xendit) would replace the claim with a checkout and a
signed webhook: the payment would be confirmed by the provider, not by an
owner reading an app, and the Confirm step would disappear for gateway
payments. It costs a fee per payment, a business registration and webhook
handling; for forty customers the manual confirm is cheaper. The claim
table would stay for transfers made outside the gateway.

## Consequences

- Anyone who gets a customer's link sees that customer's tab and can report
  payments (which an owner still checks). The page says "Don't share the link";
  Reset is the remedy.
- `App__PublicSiteUrl` must be set on the host; without it creating a link is
  a 409 rather than a wrong URL.
- The client makes its first writes, with plain `fetch` (decision 011).
