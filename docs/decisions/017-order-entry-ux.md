# 017 — Order entry: a page, a client-side preview, gated on the office menu

## Context

The owners will record 30 to 70 office orders a month, usually one drink to
one person, often while the next customer is waiting. The handover's bar:
someone who knows the screen should record a drink in under fifteen seconds.
Everything else in phase 2 (lists, detail pages, settle, merge) follows the
admin's existing patterns. The order form is the one screen where speed
decides the design.

## Decision

**The order form is a page (`/sales/orders/new`, `/:id/edit`), not a modal.**
- **Space.** It needs room for two columns: the form, and a live summary that
  stays in view. A modal would squeeze the drink list into a scrolling box
  inside another scrolling box.
- **Navigation.** A page has a URL, so the back button, a refresh and "open the
  order I just saved" behave as expected.
- **Unsaved changes.** The page can use the same unsaved-changes prompt as
  every other editor.

**Built for the keyboard, in this order:**
- **Customer** is focused on load. Its searchable select lists active
  customers, most recent orderers first, so the regulars are at the top. Typing
  a few letters and Enter chooses one.
- **Choosing a customer moves focus to the drink filter.** Type a few letters;
  Enter adds one of the highlighted drink, and Enter again adds a second.
  ↑/↓ move the highlight, and Escape clears the filter.
- **Ctrl/⌘+Enter saves from anywhere.**
- **Sensible defaults:** the date is today in Manila, the discount is None,
  and the payer is hidden behind "Paid by someone else?".

**The usual order takes ten keystrokes after the page loads**
(`kee⏎ amer⏎ Ctrl+⏎`), measured in the live smoke. Add-ons, discounts, a payer
and notes are one or two clicks each, and they're off the fast path.

**The client mirrors the pricing rules, for preview only.** `lib/pricing.ts`
reimplements `PricingService` (line totals, half-up percent in whole centavos,
fixed capped at the subtotal, a free drink taking one unit of the cheapest
line) so the total updates as drinks are added. Its tests are the API's test
cases, with the same inputs and expected numbers. **It is never the source of
truth:**
- The request carries no prices, only drinks, quantities, add-ons and the
  discount.
- The API recomputes and stores the totals.
- The saved order's detail shows the API's numbers.

If the two ever disagree, the API is right, and the preview is a bug to fix
on the client.

**New orders are taken from the current office menu, and the form refuses to
open without one.** It loads `GET /menu/versions/current?context=Office`. If
that returns 404, the form shows a blocking notice, "Publish an office menu
version first", with a link to Versions. The menu is what fixes the price
snapshot (decision 016); there's no sensible order to take without one.
Editing an order still lists any drink it already has from an older menu, at
its snapshot price, so an edit never silently drops a line.

## Consequences

- The busiest screen has no modal stacking, no hunting for a field, and a
  fixed keyboard path.
- **Two copies of the pricing rules.** Shared test cases keep them aligned,
  and a change to `PricingService` must change `lib/pricing.ts` and both test
  files together.
- **One line per drink.** Add-ons apply to every unit of that line, so "two
  lattes, one with oat milk" is two orders, or one latte line plus a note.
  That's acceptable for office sales, and it could become separate lines later.
- **No orders until the owners publish an office menu,** which is deliberate:
  production's office draft must be priced and published first (handover 12a).
