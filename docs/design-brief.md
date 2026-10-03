# Klowee Cafe: client site design brief

## Direction
"The Instagram grid turned into a website." Cobalt lettering on cream paper, floating on a sky-blue world with soft clouds. Pink and yellow appear only as stickers, tape, and small badges. Event photos are polaroids with handwritten captions. Playful, small-business, recognisable to anyone who has seen the tent or the cups.

## Palette
| Token | Hex | Use |
|---|---|---|
| `navy` | `#1C2B6B` | Body text, dark lettering |
| `cobalt` | `#2446C7` | Headings accent, primary buttons, links, tent blue |
| `sky` | `#8FCBF2` | Page background bands, cloud sections |
| `sky-light` | `#C9E6F7` | Photo placeholders, subtle fills |
| `paper` | `#F7F3E8` | Cards, hero panel, menu surfaces |
| `pink` | `#F4B8C8` | Secondary button, tape, one sticker per section max |
| `yellow` | `#FFD84D` | Sticker badges (next pop-up, new, best seller) |
| `ink-muted` | `#6B75A3` | Descriptions, captions |

Rule: navy, cobalt, paper, and sky carry 90% of every screen. Pink and yellow are accents, never large surfaces. Matcha green and ube purple come only from photos and the drinks themselves.

## Typography
| Role | Font | Notes |
|---|---|---|
| Wordmark | Real logo lettering as SVG | Fallback: Fuzzy Bubbles 700 with alternating cobalt letters (k**l**o**w**ee **c**a**f**e) |
| Headings | Fuzzy Bubbles 700 | Sentence case, lowercase feel, tight leading (1.0 to 1.05) |
| Handwritten notes | Caveat 500/600 | Polaroid captions, sticker text, small asides |
| Body and UI | Manrope 400/500/700 | 16px base, line-height 1.55, max 65 characters per line |

No all-caps labels. No eyebrow labels above headings. Prices in Manrope 700 cobalt.

## Motifs
- Paper cards: `paper` background, 18px radius, a 2px solid cobalt bottom edge instead of a shadow
- Washi tape: a small pink rectangle, slightly rotated, on the top edge of a paper card
- Stickers: yellow pill, 2px navy border, rotated 5 to 8 degrees, Caveat text
- Clouds: white rounded shapes at low opacity on sky sections, static, three per section max
- Polaroids: white frame, photo area, Caveat caption, alternating minus 2 and plus 2 degree rotation
- Dashed dividers (`sky-light` or `#C9D2F1`) between menu rows instead of solid lines
- Doodles: the Klowe and Kee illustration in the Story section, the tent sketch as an empty-state illustration

## Page structure
1. Sticker bar (top): yellow sticker text on paper strip when a next event exists. Fallback: paper strip with "no pop-up scheduled yet, book the cart for your event". Hidden if no fallback text set.
2. Nav: paper background, wordmark left, links center (story, menu, packages, pop-ups, book), cobalt pill "Book the cart" right. Mobile: wordmark plus CTA only.
3. Hero: sky band with clouds. Paper card with tape, wordmark, heading "smol pop-up cafe, big on matcha." (admin-editable), one paragraph, cobalt pill primary, pink pill secondary. Wide photo placeholder below in `sky-light`.
4. Story: paper section. Klowe and Kee illustration left, two short paragraphs right (admin-editable).
5. Menu: paper cards per category, dashed-divider rows, best-seller and new items get a yellow sticker. Reads the current pop-up menu version.
6. Packages: sky band. Three paper cards (Starter, Regular, Premium) with price, inclusions, cobalt CTA.
7. Pop-ups: "where we've been" polaroid grid of published events, upcoming events first with a "next" sticker. Expand shows gallery. Empty state uses the tent sketch and a book-the-cart prompt.
8. Book the cart (phase 2): paper form card on sky band.
9. Contact and footer: navy band, paper text, Instagram and Facebook links.

## Motion
One page-load moment only: the hero paper card settles in (fade plus 8px rise, 400ms). No per-section fade-ups. Hover states change colour, not position. Respect `prefers-reduced-motion`.

## Placeholders
Until real photos exist, every image slot is a `sky-light` block with a Caveat caption naming what goes there ("photo of the cart at Scout Market"). Placeholders must look intentional, not broken.

## Responsive
Mobile first. Container max 1120px. Menu rows stack price under name on narrow screens. Polaroid grid: 1 column under 480px, 2 under 900px, 3 above.

## Accessibility floor
Colour contrast AA for text on paper and sky (navy on sky passes; cobalt on sky does not, so cobalt text only on paper). Visible focus rings in cobalt. All images have alt text sourced from the admin caption field.
