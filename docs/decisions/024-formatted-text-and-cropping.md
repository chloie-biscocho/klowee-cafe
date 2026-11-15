# 024 — Formatted text as a Markdown subset; images cropped in the browser before upload

## Context

The owners write the site's longer texts (the hero and story bodies, the
Book line, the tab page note, event and package descriptions) in plain
boxes. They asked, by name, for justified paragraphs, and for bold, links
and lists. Separately, a photo went up exactly as taken, so the site cropped
it wherever its frame happened to fall.

## Decision

### A fixed Markdown subset, stored as plain text

The texts stay plain strings in the same columns. A small, fixed subset of
Markdown gives them meaning:

| Written | Shown |
|---|---|
| `**bold**` | bold |
| `*italic*` | italic |
| `[words](https://…)` | a link (http, https or mailto only; new tab, `noopener noreferrer`) |
| lines starting with `- ` | a bullet list |
| a blank line | a new paragraph |
| a paragraph whose first line is `::justify`, `::center` or `::left` | that paragraph's alignment |
| a single line break | a line break |

Nothing else: no headings, images, tables, code or HTML. Unknown or broken
markup shows as the characters typed, so old texts stay valid.

**Why not store HTML.** HTML from a form is a script waiting to run. Storing
it means sanitising it on the way in and trusting the sanitiser on the way
out, forever, in every place that shows it (the site, the tab page, the
admin's tables, a future email). Plain text has no such failure: the
renderer makes a tree of text nodes and a handful of elements, and React
escapes every character, so `<script>` is only ever eight characters on the
screen. A `javascript:` link is not a link because the only addresses that
become links match a strict allowlist, checked against the whole string.

**Why not a full rich-text editor.** A WYSIWYG editor (TipTap, Quill,
Lexical) is tens to hundreds of kilobytes, stores HTML or its own JSON, and
replaces the text area with a `contenteditable` that behaves differently on
phones and with screen readers. The owners need seven things; a toolbar
over an ordinary text area writes them, and the live preview shows the
result. The text still reads naturally if a formatter is ever removed.

**One renderer, two apps.** `lib/formatted.ts` is one pure function, text in
and a tree out, byte-identical in `client/` and `admin/` (they never import
from each other), with one shared file of test cases run in both. The site
and the admin's preview cannot disagree about what a text means.

**Justified text** uses `text-align: justify`, `hyphens: auto` (the page is
`lang="en"`) and `text-wrap: pretty`. A justified paragraph shows left-aligned
when its own column is under 420px wide, measured with a container query, not
the screen: narrow justified columns open wide gaps between words whether
the column is narrow because of a phone or a three-column card row.
Measured at 1280px: the hero, story, Book line and event descriptions are
justified; package descriptions (a 260px card column) and the tab page's
note (a 344px column) never are, at any width. For those two fields the
admin's preview lays out at the site's column width and says so, so the
preview never shows justify where the site won't.

### Cropping in the browser, before upload

Every image field opens a crop dialog after the file is chosen. The frame's
shape comes from how the site shows that image: 4:3 for event covers and
gallery photos (the polaroid and the photo strip), a square with a round guide
for the story photo (a circle on the site), and a free choice of shapes where
the site shows the image nowhere yet (the hero image, menu photos) or must
show it whole (payment QR codes).

**Why in the browser.** The owner is the only one who knows where the subject
is; the server can only guess at the centre. Cropping before upload also
sends fewer bytes, and needs no new API: the cropped image goes up through
the same endpoint, checks and processing (decision 009, decision 012). The
browser exports large and clean (up to 2400px on the long edge, JPEG at 0.92
or PNG), so the server's resize to 1600px and WebP at 82 stays the only real
compression. Re-cropping an existing image fetches the stored copy (storage
allows any origin, so the canvas stays readable) and uploads the result as a
new file; it crops the already-resized copy, which the dialog says when the
copy is small.

**What "Use original" is for.** It uploads the file exactly as chosen, with
no crop and no canvas step, for a photo already framed well, and above all for
payment QR codes. A QR must never be softened or cut: resampling blurs its
modules and a tight crop can remove its quiet zone, and either can stop a
banking app from reading it. For the QR field "Use original" is the default
button, and even "Crop and upload" sends the original unless the owner
actually moved the frame. If they did, the crop is drawn with smoothing off
and kept as PNG.

**The library.** `react-easy-crop` 6.2.4 (MIT): drag, zoom by slider, wheel
and pinch, rotation, keyboard moves of the frame. It loads only when the dialog
opens (a 30 kB chunk). Its frame keeps a fixed shape, so "free" means a
choice of shapes (the photo's own, square, 4:3, 3:4, 16:9), not a frame
dragged to any rectangle.

## Consequences

- No API change, no migration. Old texts render exactly as before (checked
  pixel for pixel on the homepage and a tab page).
- A text that happens to use `*` around words, `- ` at the start of a line or
  a single line break now shows formatting. None of the texts on dev do; the
  production texts were not checked (no access).
- Every image goes through one more step (the dialog). "Use original" keeps
  the old one-click path.
