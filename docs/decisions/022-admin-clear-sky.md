# 022 — The admin shares the site's look ("Clear sky"), light by default with a stored switch, one month control

## Context

Until handover 18 the admin was deliberately neutral (Manrope, off-white,
one blue accent), on the reasoning that a tool should stay quiet. The owners
compared three looks on two screens (`docs/mockups/klowee-admin-looks.html`)
and chose **A. Clear sky**: the public site's sky and navy, Karla and Young
Serif, a sky-blue sidebar with the logo. They also asked for a dark mode, and
for Orders to move by month the way the Dashboard does.

## Decision

### One look across the business

The admin uses the site's palette and type. It is the same brand the owners
show customers; working in it every day is a reason to make it pleasant, and
the loyalty card in the admin is now the very card customers see on their
tab page (handover 17a), so the two must agree.

The two apps still share no code: the admin copies the client's font files and
ports the card's markup, keeping the same component names (`SlotIcon`,
`Squiggles`, `ClubName`) so they are easy to compare. Importing across apps
would couple their builds and deploys.

### Restyled through tokens

Every colour, font and the chart's bars come from `@theme` tokens in
`admin/src/index.css`; Tailwind's default palette is switched off so a stray
colour fails to build. The names the screens already used (`canvas`,
`muted`, `accent-soft`) were kept and pointed at the new values, which is
what let the restyle be mostly one file. Components keep their markup;
shapes changed in the shared `ui/` components (pills, 18px panels, 1.5px
rules), not screen by screen.

### Light by default, dark on request

Like the site (decision 019): light for everyone whatever the device
prefers; a switch at the foot of the sidebar; the choice stored in
`localStorage` under `klowee.admin.theme` (separate from the site's) and
applied before first paint by an inline script in `index.html`. Dark values
live only under `html[data-theme="dark"]`, and Tailwind's `dark:` variant is
redefined to follow that attribute, so nothing follows `prefers-color-scheme`
(a test reads every source file to keep it so). Status colours are lightened
in dark, and darkened a shade in light, so badges and text reach AA (values
in the handover 18 report).

### One month control

`components/ui/MonthStepper` is the Dashboard's month picker, extracted at
its second use (Orders). Orders keeps its period in the URL
(`?month=2026-10`, or `?from=&to=` for a custom range): a refresh, the back
button and a shared link keep it. Arrows push history; typing a custom date
replaces it. The orders list API now returns `total` and `unpaid` for the
whole filter, so the summary line under the table is true for every page.

## Consequences

- A visual change to the brand (a token value) now reaches the admin only
  when it is copied there too; the two `index.css` files list the same
  values for the shared tokens.
- The admin ships Bowlby One and Shantell Sans, loaded only with the
  loyalty card (the customer page's chunk).
- Screens built later use the tokens and `ui/` components; no hex in components.
