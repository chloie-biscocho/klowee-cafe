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

### Cards are by invitation (handover 16a)

A card is given only to customers who ask for one, and **only cardholders
collect stamps**. `loyalty_memberships` holds one live row per (customer,
club): `Requested` or `Active`, with `joined_on`. An order stamps a club only
for an Active member, dated on or after `joined_on`. A null `joined_on` means
"since the beginning", and only the carry-over writes it. Rewards, adjustments
and hand-marked uses are refused for non-members, and a non-member's card on
the customer is empty ("none").

Giving a card (admin: **Give card**) makes it Active from today. Taking it away
is refused while it has stamps or used rewards unless forced, which voids
them. A club can **come with another**: `auto_join_from_club_id`. The Matcha
Club comes with The Coffee Club:
- when it is activated, every Coffee member gets a Matcha card dated that day;
- a Coffee card given later brings a Matcha card;
- taking the Coffee card away leaves the Matcha card.

Until it launches, The Matcha Club has no members and shows nowhere. Merging
customers keeps the card held longer.

### Carry-over from a reviewed list of paper cardholders

The `LoyaltyClubs` migration creates both clubs, with fixed ids so ContentSync
copies of the menu point at the same club in every environment, and backfills
every existing ledger entry to The Coffee Club. A data step then runs **once,
at startup, behind a marker row** (`data_migrations`).

Who holds a paper card today isn't in the data as such. A scan
(`SheetImport cardholders`, read only) lists the customers whose orders show a
card reward:
- old free-drink discounts and 50% discounts;
- notes such as "50% loyalty card", "100% loyalty card" and "free extra shot".

For each, it gives the last reward's slot and estimates the position: the
slot plus the Coffee Club drinks paid since, mod 12. The owner reviews it in a
spreadsheet: `has_card`, an optional `position` (0–11), and `card_number`,
which says which pass round the card they are on (blank or 0 = 1). The result
is `docs/import/loyalty-cardholders.csv`, the carry-over's input in every
environment. A copy ships with the API, because the Docker build sees only
`api/`, and a test keeps the two identical. Without the file, the carry-over
doesn't run and logs a warning.

The data step:
1. It maps categories by name (Coffee, Signature Coffee → Coffee; Houseblend
   and Okumidori Matcha → Matcha) and Dirty Matcha to The Matcha Club. It only
   fills blanks, and reports names it can't find.
2. It voids every old single-card entry, marked `carried_over_at`, so the
   orders it counted never stamp again. Unpaid old orders stamp when paid.
3. For each row with `has_card` = yes (names matched ignoring case; no single
   match is reported and skipped):
   - an Active Coffee Club card since the beginning;
   - one Adjustment of `12 × (card_number − 1) + position`, using the owner's
     position, else the estimate ("carried over from paper card");
   - every reward on the earlier cards, and on the current card every reward
     with slot ≤ position, marked used.

   A customer on card 4 at position 7 therefore reads "card 4, 7 of 12", with
   the 50% off used and the extra shot ready.
4. Everyone else gets no card, and nothing is written for them.

Handover 16 first carried everyone's old balance with a cap of 11. That
treated the old 10-stamp counts as if they were the paper card, which they
weren't, so the rule and its cap were replaced before anything was merged.
Development has `--Loyalty:RedoCarryOver=true`, which undoes an earlier run
(its adjustments, redemptions and cards; voided entries restored; marker
cleared) and runs the rule again.

Why at startup and not inside the EF migration: production migrations run as
an EF bundle in the deploy workflow, which executes SQL only. The carry-over
reads a file and applies rules that need tests, which only a C# service on
the test suite's SQLite allows. Startup work finishes before the API serves a
request, so no order is stamped under the new rules before the cards are
carried over. The step runs in one transaction, so a failure leaves nothing
half done and the next start tries again. It is idempotent even without the
marker: the void step runs once, carried cardholders are skipped, and mappings
only fill blanks. It never touches orders or payments.

## Consequences

- **Only the 13 customers on the reviewed list hold a card** (on dev, the same
  data as production up to the import). Everyone else earns nothing until the
  owners give them one. A card that differs from the paper one is corrected
  with Adjust stamps and Mark used / Make available.
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
