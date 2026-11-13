# Klowee Cafe: client site design brief

The public homepage as built in handover 15, "sky and layers". The approved
spec is the static mockup `docs/mockups/klowee-home-v7.html`; where this brief
and the mockup differ, this brief describes what shipped and why.

## Direction
A clear blue sky with slow clouds, the owners' round logo sticker, and the
drinks themselves as layered cups. Below it, white panels for the words and a
cream paper sheet for the menu, printed like the paper menu at the cart. Calm,
blue, small-business; pink and yellow only as marker ovals and stickers.

## Colour tokens
Defined once in `client/src/index.css` (`@theme`). **Light is the default for
every visitor**, whatever their device prefers; the dark values apply only under
`:root[data-theme="dark"]`, set by the header's theme switch and remembered in
`localStorage` (`klowee.theme`). The site stopped following the device at the
owner's request on 2026-11-13 (decision 019). The og image, favicon and static
shell are light.
Components use tokens only; no hex appears in a component.

| Token | Light | Dark | Use |
|---|---|---|---|
| `sky` | `#A9D6F5` | `#1A2452` | Page background, button text on `deep` |
| `deep` | `#1B2F7A` | `#C9D8FF` | Headings, links, primary buttons, announcement strip |
| `ink` | `#16204A` | `#EAF0FF` | Body text |
| `cloud` | `#FFFFFF` | `#2A3672` | Clouds, cup lids |
| `panel` | `#FFFFFF` | `#222D63` | Story, book, package and polaroid surfaces |
| `soft` | `#3F4C80` | `#B4C0EC` | Secondary text, dates, guest notes |
| `line` | deep at 18% | `#C9D8FF` at 22% | Package inclusion dividers |
| `ph` / `ph-ink` | `#D6ECFB` / `#4E6AA8` | `#2B3875` / `#AFC0F2` | Empty photo slots and their text |
| `paper` | `#FBF9E8` | `#12172B` | The menu sheet (the logo's cream in light) |
| `menu-ink` / `menu-soft` | `#1F3FA6` / `#5C6386` | `#9DB6FF` / `#A9B0CF` | Menu lettering and its notes |
| `menu-rule` | menu-ink at 28% | `#9DB6FF` at 30% | Dotted price leaders |
| `pink` | `#EE7C9B` | `#F08AA8` | Marker ovals, the plus/minus on drinks |
| `yellow` / `sticker-ink` | `#FFD84D` / `#1C2B6B` | same | Stickers ("best seller", "next", "latest") |

Cup layer colours are illustrations of the drinks, not brand colours; they
live in `client/src/lib/cupLayers.ts` and stay the same in both schemes.

## Type
| Role | Font | Notes |
|---|---|---|
| Headings, wordmark | Young Serif 400 | Hero h1 `clamp(2rem, 5.6vw, 3.6rem)`, line height 1.06; section titles `clamp(1.7rem, 4vw, 2.3rem)` |
| Body and UI | Karla 400/500/700 | 1.06rem, line height 1.55 |
| Hand-printed | Shantell Sans 400/500/700 | The menu sheet, polaroid names, stickers |

Self-hosted WOFF2 (latin and latin-ext) with `public/fonts/OFL.txt`. Young
Serif and Karla 400 are preloaded. Young Serif has metric-matched fallbacks
(Georgia, then Times New Roman: `size-adjust`, ascent and descent overrides
measured from the font files) so the hero heading keeps its lines when the
font swaps in. Sentence case for headings; the menu prints in lowercase.

## Motifs
- **Clouds:** a pill with two round puffs, six in the hero, sizes and drift
  distances from the mockup (`.cloud-1` to `.cloud-6`).
- **Logo sticker:** the owners' logo in a 184px circle in the hero only. The
  header is a text wordmark; there is no logo up there or on the cups.
- **Cups:** a tapered glass of three layers under a lid, no labels.
- **Marker ovals:** a hand-drawn pink loop sized to each category label,
  generated in code (`lib/ovalPath.ts`), slightly rotated, alternating.
- **Dotted leaders** between a drink and its price.
- **Polaroids:** white frame, 4:3 photo, handwritten name, date line, tilted
  ±1.5° and straightened on hover, focus or open.
- **Stickers:** yellow pill, navy border and ink, rotated −4°.
- **Tent sketch:** the line doodle for empty and error states.

## Section order
1. **Ticker:** the slim strip (option 1 of `docs/mockups/klowee-ticker-options.html`,
   chosen by the owner on 2026-11-13): full width above the header, `deep`
   background, `sky` text, centred, 9px vertical padding, medium weight, one
   line on desktop and wrapping on phones, a small round dot before the text
   that pulses slowly (opacity, 1.8 s). First match wins: the current
   announcement (linked when it has a link); else "**Next pop-up:** {name},
   {dates}, {venue}." with a "See details" link to `#popups`; else the
   `ticker_fallback` setting, the whole line linking to `#book`; else no strip.
2. **Header:** "klowee cafe" wordmark and links: our story, menu, pop-ups,
   packages, book, then the theme switch: a small `panel` pill reading
   "☾ Dark mode" on the light page and "☀ Light mode" on the dark one (the glyph
   is hidden from screen readers). Not sticky. The packages link goes when there
   are no packages.
3. **Hero:** clouds, logo sticker, `hero_heading` (h1), `hero_body`, "Book the
   cart" and "See the menu".
4. **Cup shelf:** one cup per drink on the current pop-up menu, available only,
   menu order, each drink once (a trailing "(Houseblend)" style note is
   stripped, so each matcha appears once). A single row sized to whole cups,
   between two round arrow buttons, no visible scrollbar, swipe on touch,
   arrows disabled at the ends. Hidden when there is no menu.
5. **Our story:** white panel, round photo (`story_image_url`) or an empty
   round slot, `story_heading`, `story_body` in paragraphs. Hidden when both
   heading and body are empty.
6. **Menu:** the cream sheet titled "klowee cafe pop-up menu". Two columns
   balanced by item count (one on phones). Category name in an oval, its
   description under it. A drink with a description is a disclosure (plus,
   minus); one without is a plain row. "best seller" on featured drinks. A
   blend note the category already names is dropped from the drink's name.
   No menu: "menu coming soon" on the same paper.
7. **Pop-ups:** "Where we've been" ("Where we're headed" when one is upcoming).
   Polaroids in 1, 2 or 3 columns (480px, 900px); photo count badge; "next" on
   the upcoming event, else "latest" on the newest. A click opens a panel under
   that row (dense grid flow) with dates, venue, description and a photo strip;
   one at a time; Close and Escape close it and return focus. No events: the
   tent sketch, "no pop-ups logged yet" and "Book the cart".
8. **Packages:** "Event packages", a white card per active package: name, guest
   note, price, inclusions as divided lines, "Book this". Hidden when none.
9. **Book:** centred white panel, "Bring the cart to your event", `book_body`,
   Email us, Message on Instagram, Facebook (each only when set).
10. **Footer:** "© {year} Klowee Cafe, Cagayan de Oro" and the two social links.

## Motion
Three animations, all stopped under `prefers-reduced-motion`:
- **Clouds** drift sideways and back over 24 to 40 s (transform only).
- **Cups fill** once on load: a sky-coloured cover of the glass's shape shrinks
  away, staggered 70ms per cup (capped at ten). The cover is the glass's
  sibling so the animation stays on the compositor.
- **Ovals draw in** once, when the menu sheet first enters the viewport
  (`stroke-dashoffset` from 1 to 0 on a `pathLength="1"` path).

Polaroids straighten with a 200ms transition, and the ticker's dot pulses
(opacity only, stopped under reduced motion). Nothing else moves.

## Loading, errors, layout shift
- `index.html` ships the loading render (header, hero shell with the logo,
  quiet placeholder blocks) so the first screen paints from HTML; a test keeps
  it identical to `App`'s own loading render. React replaces it in place.
- A stored dark choice is applied by a small inline script in `index.html`'s
  head, before first paint, so a returning dark visitor never sees light first.
  It also sets `<meta name="theme-color">`. The shell's switch carries the light
  label until React mounts.
- The hero holds its height while loading, so the page doesn't jump when the
  text arrives. The ticker's strip is reserved (one empty line) while loading,
  because production always has at least the fallback line; a message that
  wraps to two lines on a phone still shifts the page slightly (CLS 0.027).
- Error: a white panel with the tent sketch, "the cart's offline for a sec, try
  again?" and a retry button; header and footer stay.

## Responsive
Container 1000px with 22px gutters. At 760px and below: header links wrap and the theme switch drops below them,
smaller cups (72 × 106) and arrows, one menu column, one package column,
stacked story, and the clouds reposition (the fifth hides).

## Copy rules
Text comes from the payload or is a fixed label from the mockup. The mockup's
marketing sentences (the pop-ups' Instagram note, the packages lead, the story
slot's placeholder note) are not shipped; the pop-ups keep only "Tap a photo to
see more from that pop-up."

## Accessibility floor
Skip link, header/main/footer landmarks, every section labelled by its heading.
Visible 3px `deep` focus rings. Cups are images with the drink's name as label
and title. Arrow buttons are real buttons that disable at the ends. Polaroids
are buttons with `aria-expanded`. Decorative clouds, ovals and dotted leaders
are hidden from assistive tech.
