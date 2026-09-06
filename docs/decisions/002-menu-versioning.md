# 002 — Menu versioning

## Context

Klowee Cafe runs different menus in different settings (office deliveries vs. pop-up
cart) and changes prices over time. We also need order history to keep the price that
was actually charged, even after menus change. A single flat "menu item with a price"
table cannot express these needs without losing history.

## Decision

Separate **identity** from **pricing/availability** using a versioned model:

- **MenuItem** holds identity only — name, description, category, photo. No price.
- **MenuVersion** is a named snapshot with a **context** (`Office` or `PopUp`), an
  **effective-from** date, and a published flag. It represents "the menu we ran for X".
- **MenuVersionItem** joins a version to an item and carries the per-version details:
  **price**, availability, featured flag, sort order (unique per version+item).
- **Orders (future)** will reference **MenuVersionItem**, not MenuItem — so the exact
  price and details charged are preserved even when later versions change them.

## Consequences

- Full menu history; historical order prices stay correct forever.
- Multiple contexts (office/pop-up) coexist without duplicating item identities.
- Trade-off: reading "the current menu" means selecting the right published version,
  not a single table — slightly more query logic, paid back by correctness and history.
