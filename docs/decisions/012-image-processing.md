# 012 — Uploads are resized and re-encoded as WebP on the server, with Magick.NET

## Context

Decision 009 accepts JPEG, PNG or WebP up to 5 MB and, originally, stored the
bytes as sent. The first real event cover was a 4.1 MB phone JPEG. Every photo
the owners add from a phone will be the same size, so this is a pipeline
problem, not a one-off file.

Handover 07 solved it with SixLabors.ImageSharp 4.1.3. Handover 08 replaced the
library before the first deploy; the pipeline itself did not change.

## Decision

**`UploadService` processes every image before it reaches storage**, using
**Magick.NET-Q8-AnyCPU 14.17.2** (Apache 2.0, published 2026-09-27):

1. The existing checks run first: folder, declared type, 5 MB on the original,
   magic bytes.
2. Every read names its format in `MagickReadSettings` (`Jpeg`, `Png` or `WebP`,
   from the checked content type), so ImageMagick never sniffs the bytes and
   none of its other coders (TIFF, SVG, PDF, PostScript…) is reachable from an
   upload. Only the first frame is read.
3. `MagickImageInfo` reads the header only. Either side above **8000px** is a
   400, before any pixels are decoded.
4. Decode. If an ICC profile is embedded, convert the pixels to sRGB
   (`TransformColorSpace(ColorProfiles.SRGB)`), so a Display P3 phone photo keeps
   its colours when the profile goes.
5. `AutoOrient()` applies the EXIF rotation to the pixels.
6. Resize with the `>` geometry flag: fit 1600×1600 keeping the ratio, and only
   ever shrink.
7. `Strip()` removes EXIF, XMP, IPTC and ICC.
8. Encode WebP at quality **82**. The key always ends in `.webp`, the content
   type is always `image/webp`, and the returned `size` is the stored size.
9. Undecodable bodies, and anything that trips a resource limit, are a 400.

**Resource limits** are set once, process-wide, in `UploadService`'s static
constructor: width and height 16,384 (a backstop above the readable 8000 check),
area 8000 × 8000 pixels, memory 256 MB, disk cache 512 MB. A hostile file that
somehow passes the header check still cannot take the container's memory.

Objects uploaded before handover 07 are untouched. Re-upload one in the admin to
get the processed version.

## Why on the server

- **One place, every client.** Resizing in the browser would be a courtesy the
  API can't rely on, the same argument decision 009 makes for validation.
- **The metadata has to go anyway.** Phone JPEGs carry GPS coordinates. Removing
  them must happen where it can't be skipped.
- **It happens once.** About 0.6 s of CPU at upload time for a 5000px photo;
  every page view after that gets the small file.

## Why WebP, 1600 and 82

- **WebP** is supported by every browser the site targets, typically 25–35%
  smaller than JPEG at equal visual quality, and handles transparency, so one
  output format covers all three inputs.
- **1600px** is the widest slot on the page (the hero, 1120px container) at about
  1.5× density. Anything bigger is bytes nobody sees.
- **Quality 82** is about where lossy WebP stops showing artefacts on photos at
  normal viewing size. It is a constant (`UploadService.WebpQuality`).

## Why not Supabase image transformations

They are a paid-plan feature, billed per origin image, and would leave the 4 MB
original, GPS metadata included, as the stored object.

## Why Magick.NET

- **ImageSharp 4.x is disqualified by its build-time licence check.** Since 4.0
  the package fails any Release build without a Six Labors licence key. The
  project qualifies for the free tier, but a key that must exist on every build
  machine (CI, Render's Docker build) for a free open-source library is a
  deployment dependency nobody should have to remember.
- **ImageSharp 3.x has open advisories.** Every version up to 4.1.1 is affected
  (GHSA-gwg2-r3hj-4w44, -j3p4-wp97-rph4, -j9gm-c75j-xc9q, -jjfr-hcj7-qf5w,
  -wmxv-xphr-5c9g; three high). The fixes exist only in 4.1.2 and later.
- **Magick.NET has API parity.** Every step above is one call, and it ships
  EXIF orientation, ICC conversion and WebP encoding built in. Apache 2.0, no key,
  no build-time check. `dotnet list package --vulnerable` reports nothing.
- **SkiaSharp (MIT) is the fallback.** It is fast and permissively licensed, but
  EXIF orientation and ICC handling are left to the caller, so the pipeline would
  grow by hand-written code.

## Consequences

- **Native binary size.** Magick.NET wraps the native ImageMagick library: about
  37 MB for linux-x64. A framework-dependent publish without a runtime identifier
  copies every platform's native build (about 240 MB), so the Dockerfile
  publishes with `-r linux-x64`. The library needs only glibc
  (`libc`, `libm`, `libpthread`, `libresolv`), which the `aspnet` base image has.
- Stored photos are typically 100–400 kB, with no location data.
- At the 8000px cap a decode can use up to the 256 MB memory limit before
  spilling to the capped disk cache. That's fine for two uploaders, and it's the
  number to watch on a small host.
- Animated WebP is flattened to its first frame.
- ImageMagick is a large native codebase with a long CVE history. Naming the
  decoder on every read is the main mitigation, and upgrades should follow
  Magick.NET releases.
