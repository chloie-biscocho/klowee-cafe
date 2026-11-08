# 016 — Office orders: derived status, snapshot prices, payments per order, a loyalty ledger

## Context

Phase 2 moves the owners' office-sales spreadsheet into the app. That means
30–70 orders a month, mostly one drink, often put on a tab and paid days or
weeks later, sometimes partly, sometimes several orders with one transfer,
over six payment channels. A stamp card rewards paid drinks with a free one.
Prices change over time. Pop-up sales stay in Loyverse.

## Decision

**Tables** (all with the usual audit and soft-delete columns, decision 003):
`customers`, `orders`, `order_items`, `order_item_add_ons`, `payments`,
`loyalty_entries`. Migration `Phase2Sales`.

```
customers ─┬─< orders (customer_id)          the drinker: earns the stamps
           └─< orders (payer_id, nullable)   who pays when it isn't the drinker ("Prince c/o Kee")
orders ──< order_items ──< order_item_add_ons
   │            └── menu_version_item_id ──> menu_version_items   (decision 002)
   └──< payments
customers ──< loyalty_entries >── orders (nullable)
```

### Payment status is derived, never stored

`Unpaid` (nothing paid), `Partial`, `Paid` (paid ≥ total) are computed from
the order's live payments on every read (`SalesQueries.StatusFor`). A stored
status would be a second copy of a fact the payments already hold, and the
two could disagree: a payment deleted, a status not updated. The order's
money (`subtotal`, `discount_amount`, `total`) *is* stored. It's the price
agreed at the time, recomputed by `PricingService` whenever the order is
edited, and is what reports add up.

A zero-total order (a free drink alone) is `Paid` with no payments.

### Prices are snapshots

`order_items.unit_price` and `order_item_add_ons.unit_price` are copied from
the menu version item and the add-on when the line is created, and never
recomputed. The line still points at the exact priced row it was sold from
(decision 002), for its name and history. A PUT that keeps a drink on the
order carries that line's snapshot over, so editing an order never reprices
it. Only a newly added line takes today's price. Orders may use items from
any version, and the detail warns when an item isn't on the current office
menu. Order reads look the referenced rows up with `IgnoreQueryFilters()`,
because a draft version's items are replaced (soft-deleted) when it's
edited.

### One payment belongs to one order; settle splits a transfer

A payment always belongs to a single order, so each order's status, balance
and stamps stay its own. `POST /customers/{id}/settle` records "paid 500 for
last month". It walks the customer's tab, oldest `ordered_on` first, gives
each order what it owes until the money runs out, and writes one payment per
order touched, all in one transaction. It refuses (409) more than the tab
owes. The **tab** is every order whose payer is the customer: their own,
unless someone else pays, plus any they cover. Overpaying a single order is
409 too.

An order with payments can't change its items or discount (409): its total
is what was paid against. Remove the payments, then edit.

### Loyalty is a ledger

`loyalty_entries` holds signed deltas: `Earned` (+drinks), `Redeemed`
(−`loyalty_stamps_per_reward`) and `Adjustment` (the owners' corrections).
The balance is their sum, and available rewards = ⌊balance ÷ stamps per
reward⌋. A counter column can't say *why* it holds 7, can't be reversed
precisely when a payment is deleted, and drifts the first time two writes
race. A ledger answers all three, and the owners can read it.

After every change to an order or its payments, `LoyaltyService.ReconcileAsync`
makes the order's entries match the order as it now stands:

- **Paid** → one `Earned` entry worth its drinks (quantities summed, minus
  the free one on a LoyaltyFree order), owned by the drinker.
- **Carries `LoyaltyFree`** → one `Redeemed` entry. Applying it requires at
  least one available reward, not counting this order's own entries.
- Anything that stops being true (dropped below Paid, discount removed,
  order deleted, drinker changed) is **voided**, meaning soft-deleted. It
  leaves the visible ledger but stays in the table as history.

`loyalty_stamps_per_reward` is a site setting (default 10, whole number
1–1000, never sent to the public site).

### Pricing has no dependencies

`PricingService` takes snapshot prices, quantities and the discount, and
returns line totals, subtotal, discount and total. It has no database and no
clock. Percent rounds half-up to the centavo; a fixed discount is capped at
the subtotal. `LoyaltyFree` frees one unit of the line with the lowest
per-unit price, add-ons included. Which discounts are *allowed* (0–100%, 0 to
the subtotal, a reward available) is the order service's job.

## Consequences

- History can't change under the owners. Totals, statuses and stamps are
  always consistent with the payments, because two of the three are derived
  from them.
- Lists and reports compute status and sums in memory. The test suite's
  SQLite can't sum, compare or sort decimals and timestamps in SQL. At tens
  of orders a month, loading them costs nothing, but a list of tens of
  thousands of orders would need SQL-side aggregation (fine on PostgreSQL).
- A settle is several payments, not one. The shared `reference` and
  `paid_on` tie them together for the owners.
- No row locking: two simultaneous payments on the same order could together
  exceed its total. With two owners entering payments, this is accepted. A
  `SELECT … FOR UPDATE` is the fix if it ever happens.
- Voided ledger entries accumulate as orders are edited and payments
  corrected. They're cheap, and they're the audit trail.
- The new tables aren't in ContentSync's list (decision 014): sales are
  operational data and stay in the environment where they were recorded.
