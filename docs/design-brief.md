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
- **Clouds:** a pill with two round puffs. Six in the hero, sizes and drift
  distances from the mockup (`.cloud-1` to `.cloud-6`). Below the hero, one sky
  behind every section from the shelf to Book (handover 19): 19 clouds on a
  computer, 9 on a phone, from a fixed list (`lib/pageClouds.ts`), scattered
  rather than tiled: mostly hanging off the edges, a few large and very soft
  (opacity 0.4 to 0.5), only small faint ones near the middle. Big clouds drift
  slower (44 to 52 s) than small ones (24 to 28 s). The footer stays clear.
- **Logo sticker:** the owners' logo in a 184px circle in the hero only. The
  header is a text wordmark; there is no logo up there or on the cups.
- **Cups:** a tapered glass of three layers under a lid, no label at rest.
  At rest they stand straight in one level line. Hovered or tapped (for
  1.2 s on touch; a swipe lets go), a cup lifts 6px over a soft shadow, leans
  about 4° (alternating by position, so neighbours lean different ways) and
  shows its name in a small white tag. With the shelf focused from the
  keyboard, every cup lifts and shows its name but stays straight
  (handover 19; tilt on hover since 19a).
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
   arrows disabled at the ends; an arrow pages exactly one window. Cups are
   74 × 106 (58 × 85 at 760px and below) with 24px (13px) gaps, 20% smaller
   than at launch, so a computer shows 8 instead of 7. Hidden when there is no menu.
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
9. **Book:** a white panel. With the **booking request form** on (handover 20;
   `booking_form_enabled` isn't "false"), layout **B, one wide form**
   (`docs/mockups/klowee-booking-layout-sample.html`, handover 20a; the owner
   chose B over A after seeing A built):
   - **A top row:** on the left "Bring the cart to your event" (left-aligned),
     `book_body`, `booking_form_note` when set, and the **steps**
     (`booking_steps`: up to three lines, a real ordered list with the
     numbers in `sky` circles); on the right "or message us" with Email us,
     Instagram, Facebook (each only when set), at the row's
     bottom. The row wraps (buttons under the words) when the words would be
     narrower than about 20rem.
   - **The form across the panel:** six columns from 820px (name and how to
     reach you a half each; event date, guests and package a third each, or
     date and guests a half each without packages; venue and anything else
     full width), two columns from 520px, one below. Then the send button
     with "Your details are only used to reply about this booking." beside
     it (full-width button below 520px).
   - **The fields:** hint "Mobile number, Messenger or Instagram" under its
     input, event date earliest today in Manila, guests optional, package
     the active ones and "Not sure yet", anything else optional with a
     count. Field shape from the tab page's Pay form (`cloud` fill, 1.5px
     `line` border, 12px corners). Errors sit under each field in bold
     `deep` (no red: the palette has none), with a 2px `deep` border and
     focus on the first. Sending, the button says so; sent, a `sky` panel
     "Request sent. Klowee will get back to you." takes focus in the form's
     place, with Send another.
   - A package card's "Book this" scrolls to the form with that package
     chosen. The form is its own chunk; its space is reserved on the
     placeholder and the form (862px below 520px, 657px from 520px, 518px
     from 820px; below 820px 87px less without packages).
   With the form off, the section is exactly as before handover 20: centred
   heading, `book_body`, and the buttons.
10. **Footer:** "© {year} Klowee Cafe, Cagayan de Oro" and the two social links.

## Motion
Three animations, all stopped under `prefers-reduced-motion`:
- **Clouds** drift sideways and back over 24 to 40 s (transform only).
- **Cups fill** once on load: a sky-coloured cover of the glass's shape shrinks
  away, staggered 70ms per cup (capped at ten). The cover is the glass's
  sibling so the animation stays on the compositor.
- **Ovals draw in** once, when the menu sheet first enters the viewport
  (`stroke-dashoffset` from 1 to 0 on a `pathLength="1"` path).

The page's clouds use the hero's drift. Cups lift with the polaroids' 200ms
ease; under reduced motion they stay straight and never lift, though a
hovered cup still shows its name.

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
smaller cups (58 × 85) and arrows, one menu column, one package column,
stacked story, the hero's clouds reposition (the fifth hides), and the page sky drops to 9 clouds, each at most 70% of the screen wide.

## Formatted text (handover 21)
The owners' longer texts (hero body, story body, the Book line, the tab
page's pay note, event and package descriptions) can use a small Markdown
subset (decision 024): bold, italic, links (http, https, mailto; new tab),
bullet lists, line breaks, and per-paragraph alignment (`::justify`,
`::center`, `::left`). Plain text looks exactly as before.
- **Links** are `deep`, medium weight, underlined. **Lists** have `deep`
  disc markers, indented 1.3em. Bold and italic use the face's own weights.
- **Justify** uses hyphenation (`lang="en"`) and `text-wrap: pretty`, and
  only when the paragraph's own column is at least 420px wide (a container
  query); narrower, it is left-aligned so no wide gaps open between words.
  Centre applies at every width.
- **Package descriptions** now show on their cards, under the guest note and
  above the price; a package without one looks as before.
- **Where justify shows** (measured at 1280px): hero, story, Book line and
  event descriptions; never package descriptions (260px column) or the tab
  page's note (344px), and nothing at phone widths.

## Copy rules
Text comes from the payload or is a fixed label from the mockup. The mockup's
marketing sentences (the pop-ups' Instagram note, the packages lead, the story
slot's placeholder note) are not shipped; the pop-ups keep only "Tap a photo to
see more from that pop-up."

## The tab page (handover 17)
A customer's private page at `/tab/{token}`, layout **C. Receipt** of
`docs/mockups/klowee-my-tab-options.html` with two cards as tabs, chosen by the
owner. Same tokens, fonts and theme switch as the homepage; light by default.
- **Header:** logo, "klowee cafe", "hi, {name}", the theme switch.
- **Receipt:** the menu sheet's `paper` and `menu-ink` in Shantell Sans (its
  dark treatment in dark): "your tab", a dotted-leader row per unpaid order
  with a second line for the date and "N paid", a ruled "you owe ₱…". Nothing
  owed: "you're all paid up". Reports waiting for Klowee are yellow sticker
  chips; a rejected report shows its reason.
- **Pay dialog (handover 17b):** a native `<dialog>` (Karla) with a fixed
  header (title, ✕) over a scrolling body. From 760px it is about 760px wide
  in two columns: left, the method switch, the QR at 300px square on a
  fixed-white card (`card-slot`) with a quiet border and padding, and Save QR;
  right, the amount large, the account name and number with Copy, the page
  note, the form and the buttons. On a phone it is a sheet at the foot of the
  screen (rounded top, 12px side gutters, up to 92% of the height) in one
  column: switch, amount, QR (up to 320px), Save QR, account, note, form. The
  QR is never drawn larger than its natural size; a method without a QR shows
  its details across the width. It rises in on open, with no motion under
  reduced motion.
- **Loyalty cards:** drawn from the data on the printed card's cream face,
  which stays cream in both themes (tokens `card`, `card-slot`, `card-ink`,
  `card-pink`, `cobalt`, `matcha` #437234, `accent-pink`, `bean`, `leaf`,
  three `squiggle-*`). The club's name in **Bowlby One** (`font-display`,
  self-hosted, loaded only on this page; never below 24px, its large-text
  size), with the club's accent letter (the U of CLUB) in `accent-pink`
  (3.28:1). Slots in two rows with a bean heart (Coffee) or leaf (Matcha),
  reward slots pink with dark ink, the last in the club colour with cream
  lettering. A filled slot gets a rubber stamp in real text, **SIPPED** or
  **CLAIMED**: Bowlby One, solid ink in a 2.5px outline on a light wash
  (cobalt on cream; cream on the club-colour slot), allowed to overhang the
  circle, tilted by the admin's `stampTilt`; measured 5.95:1 over pink and
  about 7:1 elsewhere. The date above as a bold `M/D`. Circles are about
  46px wide on a 360px phone and 83px at 1440px. Finished cards stack behind
  the current one with "Card 2 of 3" and arrows (and swipe). Without a card,
  the slots and squiggles fade while the lettering keeps full contrast.
- **Layout:** one column up to 900px (max 480px wide, small side padding so
  the circles stay large); two columns above it, up to 1200px, receipt and
  payments left (40%), cards right (60%). A long receipt shows four orders
  and "Show all N orders" so Pay stays near the top. Payment dates carry the
  year when not this year or after today.
- **Sky and scene:** the homepage's `Clouds` twice in a fixed layer behind the
  page. At the foot, `CartScene` (its own lazy chunk): awning, three cups
  refilling with the cup-cover technique, steam, the round logo as the sign,
  and `sign` / `characters` slots for animated Klowe and Kee later. Fixed
  aspect ratio, transform and opacity only, static under reduced motion,
  decorative (`aria-hidden`).
- **Dead link:** "this link isn't active anymore" with "Ask Klowee for a new
  one". Network error: the homepage's error card with retry.

## Accessibility floor
Skip link, header/main/footer landmarks, every section labelled by its heading.
Visible 3px `deep` focus rings. Cups are images with the drink's name as label;
the visible name tag is hidden from assistive tech, and no cup is a tab stop. Arrow buttons are real buttons that disable at the ends. Polaroids
are buttons with `aria-expanded`. Decorative clouds, ovals and dotted leaders
are hidden from assistive tech.
