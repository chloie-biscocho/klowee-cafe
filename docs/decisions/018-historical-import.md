# 018 — Historical import: through the API, with imported menu versions and owner-edited mappings

## Context

The owners kept sixteen months of office sales (May 2025 – Nov 2026) in a
Google Sheets workbook: about 730 rows across monthly tabs, alongside drafts,
working copies, pop-up, costing and supplier tabs. The sales admin (handovers
11–12) should start with that history. It needs customers with their open tabs
and stamps, revenue per month on the dashboard, and orders that point at real
menu prices. The import is `tools/SheetImport`.

## Decision

**Everything is written through the admin API, never SQL.**
- **The rules the import needs live in the services.** Snapshot prices on order
  lines, derived payment status, the loyalty ledger's `ReconcileAsync`, the
  reward check, "the tab belongs to whoever pays", and the rule that an archived
  item can't be priced into a version.
- **SQL would have had to re-implement each of those.** Every row would also
  need hand-typed ids, timestamps and soft-delete fields, in two databases.
- **The API showed what the plan really needed.** It refused archived drinks in
  a version, so the tool unarchives them for a moment and archives them again.
  It also refused a free drink without a reward, which is why the replay has to
  run in date order.

**Historical office versions, one per month in which a regular price changed.**
- **Why versions.** Decision 002 says every order line points at a real version
  item with a snapshot price. Old prices (Spanish Latte ₱120 in 2025, ₱150 from
  March 2026) aren't in today's menu, so the import creates
  `Imported office menu (from YYYY-MM-01)` versions. They are Office,
  **unpublished**, and hold every drink sold so far at its regular price.
- **What sets a month's price.** The month's most common undiscounted price,
  the higher on a tie. A month gets a version only when some price changed or a
  drink first appeared: 18 versions in all.
- **Why not one per change date.** That made about 40 versions, most from one-off
  prices. The current office and pop-up versions are not touched.
- **Retired drinks.** Drinks no longer sold (Her-She Latte, Caramel Macchiato,
  …) are created in a "Retired drinks" category and archived once the versions
  are priced, so history has them and no menu shows them.

**Every order's total equals the sheet's Total, or the gap is listed.**
- **Add-ons and discounts.** Add-ons snapshot today's prices; the API has no
  versioned add-ons. An order uses its version's price plus add-ons. When the
  sheet charged less, the difference is recorded as a discount: the notes' own
  percent or "less N" when it reproduces the total, otherwise a fixed amount
  with the reason in the plan (e.g. "oat milk was ₱20 then").
- **Free drinks.** A free drink uses the loyalty reward only if the replay says
  the customer has one; otherwise it's a fixed discount and flagged, as the
  handover asked.
- **When the sheet charged more.** The order can't carry the excess (the API has
  no surcharge). The excess is listed as "not carried": ₱446 over 28 rows,
  mostly ₱50 oat milk and ₱5 pop-up prices.

**Mapping files, edited by the owner, rather than fuzzy matching.**
- **What they hold.** `docs/import/customers.csv` maps raw name → customer, and
  `docs/import/drinks.csv` maps (raw drink, unit price) → menu item.
- **The tool only suggests.** It pre-fills the obvious cases with a hint ("one
  letter from Yelly", "short for Klowe"). The owner decides.
- **Why not match automatically.** The owners knew things no matcher could:
  - Kee is Kesha, and Klowe is Chloie.
  - Spanish Spice was renamed Cinnamon Cloud.
  - ₱180 Banana Matcha was the Houseblend one.
  - Clark 1–4 are one Clark.
- **Blank is a decision too.** A blank mapping means the rows using it are
  skipped and listed, never guessed. Re-running `parse` keeps every value the
  owner wrote.

**Idempotent, and reversible.**
- Each order's notes start with `[import:<tab>:<row>]`, and a re-run skips any
  row whose marker exists.
- `--rollback` soft-deletes every marked order through the API. Their payments
  and stamps go with them, and orders typed in the admin are untouched.
- Versions, retired items and customers stay, and are reused by the next run.
- Payments are made straight after their order, so a stopped run resumes
  cleanly. An order left without its payment gets it on the next run.

## What was skipped, and why

- **21 rows with no drink:** oat milk, cups, milk or powder charged on their
  own. An order needs a drink. ₱385.
- **18 ₱0 rows with no free-drink marker:** the owners' own drinks and
  complimentary ones. ₱0.
- **1 row left blank by the owner:** an espresso shot for a photo. ₱0.
- **Not imported at all:**
  - the pop-up, costing, supplier and tracker tabs;
  - nine duplicate or draft monthly tabs. One authoritative tab per month is
    read: `May(Manual)`, then `June 2025` … `Nov 2026`.

## Consequences

- **The dashboard has history from May 2025.** Each month's revenue is within
  ₱0–₱245 of the sheet's own cell, with every difference itemised.
- **The admin's version list gains 18 unpublished "Imported office menu"
  entries.** They are history, not drafts to publish.
- **Stamps come from the replay under today's `loyalty_stamps_per_reward`
  (10).** The paper loyalty cards of 2025 may have counted differently. The
  owners can adjust a balance from the customer page.
- **Partial payments mentioned only in notes aren't imported.** Two rows say
  "partial 30". Those tabs show the full amount until the owners record the
  payment.
