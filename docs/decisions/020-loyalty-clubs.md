# 020 — Loyalty clubs: cards of slots derived from a ledger, rewards as vouchers

## Context

Decision 016 gave the office sales one loyalty rule: every paid drink earns a
stamp, ten stamps buy a free drink, and the free drink is an order-level
discount (`LoyaltyFree`) paid for with a −10 ledger entry. The owners' real
card works differently, and a second card is coming. On 2026-11-13 they
confirmed the rules:

- A **club** is a card of 12 slots. The first is **The Coffee Club**; **The
  Matcha Club** launches later.
- Every **paid** drink of a club fills that club's next slot.
- Three slots carry a reward. Coffee Club: slot 5 **50% off**, slot 8 **free
  extra shot**, slot 12 **free drink**. A drink that uses a reward still fills
  its slot. After slot 12 the card starts again.
- A drink's club comes from its **menu category**, with a per-item override.
  Coffee and Signature Coffee feed The Coffee Club, both matcha categories The
  Matcha Club, and Dirty Matcha (in Coffee) The Matcha Club. Non-coffee and
  Refreshers earn nothing.

## Decision

### Slots are derived; rewards are vouchers

The ledger of decision 016 stays: `loyalty_entries` holds signed stamps, now
per club (`club_id`). Nothing stores where a customer is on a card. For a
customer and a club, with `S` = the sum of their live entries (floored at 0):

```
cycle    = ⌊S ÷ slots⌋           which pass round the card
position = S mod slots           slots filled on the current card
earned   reward on slot s in cycle c, once S ≥ c × slots + (s − 1)
used     a live loyalty_redemptions row for (customer, club, cycle, reward)
available = earned and not used, in any cycle
```

A used reward is a row in `loyalty_redemptions`, with at most one live row per
(customer, club, cycle, reward), so each reward is used once per card. It
points at the order line that used it, or at nothing when the owners mark it
by hand from a paper card. The ledger counts drinks; the redemptions count
vouchers. Neither can drift from the other because neither is a copy of the
other.

The other model, a counter that goes down when a reward is spent (decision
016's −10), doesn't fit a card whose rewards sit on slots. Spending a reward
doesn't take the stamps back: the drink that used the 50% off still fills slot
5. A position computed from the ledger, plus vouchers that are earned at
thresholds and consumed once, says exactly that.

`LoyaltyCard` (`Services/LoyaltyCard.cs`) does this arithmetic. It is pure and
is tested on a table of `S` values across cycles.

### Rewards unlock on paid stamps

A reward is earned when the slot before it is filled, and slots fill only when
an order becomes **Paid** (decision 016's reconcile, unchanged in spirit). A
drink on a tab doesn't unlock the next reward until the tab is paid. That is
the owners' rule ("every paid drink"), and it keeps one source of truth: stamps
follow payments, and rewards follow stamps. The customer page shows "N drinks
on the tab will stamp once paid" so nobody is surprised.

Applying a reward is checked when the order is **saved**, not when it is
paid. The redemption is written then, so the reward can't be promised to two
orders. Rewards and stamps belong to the **drinker**, whoever pays.

### Unused rewards persist

An earned reward that isn't used stays available on later cards ("from card
1"). The owners don't want customers to lose a free drink because they forgot
to ask for it, and nothing in the paper card expires. Using an older card's
reward first (`Available` is ordered oldest card first) keeps it simple.

### Line rewards before order discounts

A reward sits on one order **line** and affects one unit of it:

- **PercentOff:** that percent of one unit's drink price; add-ons charged.
- **FreeAddOn:** one unit's copy of the add-on free when the line carries it;
  otherwise the add-on is given at ₱0 and nothing comes off.
- **FreeDrink:** one unit's drink price; add-ons still charged, which is how
  the owners have always done it.

Line totals are stored after their reward, and the subtotal adds them up.
An order-level Percent or Fixed discount then applies to that subtotal.
`PricingService` stays pure. `lib/pricing.ts` in the admin mirrors it, with
the same cases.

`LoyaltyFree` stays in the enum so the 21 imported free-drink orders read
correctly, but new and edited orders can't use it (400).

### Category mapping with an item override

`menu_categories.loyalty_club_id` says which club a category's drinks stamp.
`menu_items.loyalty_mode` overrides it: `Inherit` (the default) uses the
category's club, `None` earns nothing, and `Club` uses
`menu_items.loyalty_club_id`. One rule, `LoyaltyClubMap.Resolve`, answers
"which club" everywhere: stamping, pending drinks, reward checks, and the menu
version item DTO the order form reads.

The club is resolved when the order **becomes Paid** and stored on its ledger
entry. Moving a category later doesn't rewrite stamps already earned. A club
that isn't active earns nothing and isn't shown. Its first activation dates it
(`activated_on`, business time zone), and an order dated before that never
stamps it: there is no backfill.

### Carry-over from the old card

The `LoyaltyClubs` migration creates both clubs, with fixed ids so ContentSync
copies of the menu point at the same club in every environment. It backfills
every existing ledger entry to The Coffee Club. A data step then runs
**once, at startup, behind a marker row** (`data_migrations`):

1. It maps categories by name (Coffee, Signature Coffee → Coffee; Houseblend
   and Okumidori Matcha → Matcha) and Dirty Matcha to The Matcha Club. It only
   fills blanks, and reports names it can't find.
2. For each customer with live entries:
   - their old balance `B` (floor 0, cap 11);
   - every old entry voided and marked `carried_over_at`;
   - one Adjustment of `+B` ("carried over from the old card");
   - a redemption on card 1 for every Coffee Club reward on a slot ≤ `B`, so
     nobody gets a reward twice.

Why at startup and not inside the EF migration: production migrations run as
an EF bundle in the deploy workflow, which executes SQL only. The carry-over
needs the old-balance rule, the cap and the reward thresholds, and it needs
tests on a fixture (balances 0, 4, 6, 11, 23), which only a C# service on the
test suite's SQLite allows. Startup work finishes before the API serves a
request, so no order is stamped under the new rules before the old card is
carried over. The step runs in one transaction, so a failure leaves nothing
half done and the next start tries again. It is idempotent even without the
marker: customers already carried over are skipped, and mappings only fill
blanks. It never touches orders or payments.

An order the carry-over already counted never stamps again, even if its
payments are redone later; `carried_over_at` on its old entry says so.

## Consequences

- **Customers with more than 11 old stamps are capped at 11.** On dev that is
  15 of 61; one had 42 stamps (4 free drinks under the old rule) and now has
  one free drink waiting. A customer at exactly 10 had a free drink under the
  old rule and now needs one more. The owners correct individual cards from
  their paper cards (Adjust stamps, Mark used).
- `GET /customers` returns `clubs[]` instead of `loyaltyBalance` and
  `availableRewards`. The customer-facing page (handover 17) reads the same
  shape.
- `loyalty_stamps_per_reward` is retired, and the migration soft-deletes its
  row.
- Rewards are replaced **by slot** when a club is saved. Editing slot 5's
  percent keeps every customer's use of slot 5; removing slot 5 orphans those
  uses as history.
- A merge adds the two customers' stamps club by club. When both used the same
  reward on their first card, the uses are renumbered onto cards 1 and 2, so
  every use survives.
- `loyalty_entries.club_id` keeps a database default of The Coffee Club's id.
  It covers the minutes between the deploy's migration and the new API going
  live, when the old API still writes stamps without a club. The model has no
  default, so the new API always writes the column itself.
- A club created by hand exists in one environment only; ContentSync of
  categories that use it needs the same club on the other side (README).
