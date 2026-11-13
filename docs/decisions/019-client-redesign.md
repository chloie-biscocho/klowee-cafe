# 019 — Client redesign: "sky and layers"

## Context

The first public site (handovers 05 to 07, design brief of September 2026) was
"sky, paper and stickers": cobalt lettering on cream paper cards, Fuzzy Bubbles
headings, washi tape and stickers everywhere. The owners lived with it for two
months and then reviewed their own Instagram grid with Claude (vault page
`klowee-redesign-mockups`). The grid is far bluer and calmer than the site was:
real sky behind almost every post, the round Klowe-and-Kee logo, and the drinks
as the star. The owners chose the "sky and layers" mockup on 2026-11-11 and
approved version 7 (`docs/mockups/klowee-home-v7.html`).

## Decision

Rebuild the client's look to match v7, on a branch (`redesign/sky-and-layers`)
with a Vercel preview, keeping its architecture (decision 011: one page, one
request, no router, no store).

**Kept from the old site, because the owners asked for them or they worked:**
- **Polaroids for pop-ups.** The owner asked to keep them. Improved: the name
  and date on separate lines, a photo-count badge so a card reads as tappable,
  "latest" when nothing is upcoming, a gentler tilt that straightens on focus,
  and a visible Close button.
- **The book section** as it was: heading, the book line from Settings, and
  Email, Instagram and Facebook buttons. No form yet.
- **The yellow "best seller" sticker** on the menu.
- **The ticker**, restyled as a slim deep-blue strip with a pulsing dot
  (handover 15a; the owner chose it on 2026-11-13 over a paper note and a cloud
  message). Same rule as before: the announcement, else the next pop-up, else
  the fallback line from Settings, else nothing. Handover 15 first dropped the
  fallback line; the owner wanted it kept.
- Placeholders, the tent-sketch empty states, and every section surviving
  missing data.

**Cup layers live in the client for now** (`client/src/lib/cupLayers.ts`).
- **They're illustration, not data.** Which colours suggest "ube oat latte" is a
  drawing decision, read by nothing but the shelf.
- **The admin is the wrong place yet.** Putting them there would mean three
  colour pickers and two stop positions per menu item for owners who haven't
  asked for it.
- **A new drink never breaks the page.** An unknown drink gets a neutral default
  cup, so a new item appears on the shelf at once and can get its own colours
  in a one-line change.

If the owners later want to choose colours themselves, the map becomes three
nullable columns on `menu_items` and the same lookup falls back to it.

**Light by default, dark on request** (handover 15b). Handover 15 had no
switch and followed the device's colour-scheme setting. On 2026-11-13 the owner
asked for light as everyone's default, with a switch for visitors who want
dark. So:
- **The device setting is ignored.** No `prefers-color-scheme` rule remains;
  dark tokens apply only under `:root[data-theme="dark"]`.
- **The switch** sits at the right end of the header nav, as in the mockup, and
  is remembered in `localStorage` (`klowee.theme`: `light` or `dark`; nothing
  stored means light; blocked storage just means it isn't remembered).
- **No flash.** A small inline script in `index.html`'s head applies a
  stored `dark` before first paint. Its key and colour duplicate
  `client/src/lib/theme.ts`; a test runs the script so they can't drift.

**Category descriptions are real data** (`menu_categories.description`,
handover 15 Part 1). The two matcha blends need a line explaining the
difference, and that line belongs to the owners, not the code.

## Consequences

- **The logo is now an image file** (`public/logo.jpg`, 440px). The old
  `Wordmark` probe for `logo.svg` is gone; the header is plain Young Serif text.
- **Three animations exist:** clouds, the cup fill and the oval draw-in. All
  three stop under reduced motion, and only the clouds run continuously.
- **The first screen is now static HTML.** `index.html` carries the page's
  loading render, kept identical to `App`'s by a test. This plus an early API
  preload keeps first paint independent of the script.
- **Lighthouse's default simulated score now mostly reflects API latency.**
  The page's one request starts before first paint, by design. Measured like
  for like, the redesign scores the same as the old site (handover 15 report).
