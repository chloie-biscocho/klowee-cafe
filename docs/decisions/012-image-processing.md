# 012 — Uploads are resized and re-encoded as WebP on the server

## Context

Decision 009 accepts JPEG, PNG or WebP up to 5 MB and stores the bytes as sent.
The first real event cover was a 4.1 MB phone JPEG. Lighthouse flagged about
2.1 MB of avoidable download on the public page, and on a throttled mobile
profile the cover had not arrived when the test timed out. Every photo the
owners add from a phone will be the same size, so this is a pipeline problem,
not a one-off file.

## Decision

**`UploadService` processes every image before it reaches storage**, using
**SixLabors.ImageSharp 4.1.3**:

1. The existing checks run first: folder, declared type, 5 MB on the original,
   magic bytes.
2. `Image.IdentifyAsync` reads the header only. Either side above **8000px** is
   a 400, before any pixels are decoded.
3. Decode with a configuration that knows only JPEG, PNG and WebP, so none of
   ImageSharp's other decoders (TIFF, BMP, GIF and so on) is reachable from an
   upload. Embedded ICC profiles are converted to sRGB, so a Display P3 phone
   photo keeps its colours when the profile is dropped. Undecodable bodies are
   a 400.
4. `AutoOrient()` applies the EXIF rotation to the pixels.
5. If the longest edge is over **1600px**, resize to fit 1600×1600 with the aspect
   ratio kept. Smaller images are never enlarged.
6. Drop EXIF, XMP, IPTC and ICC metadata.
7. Encode as lossy **WebP, quality 82**. The stored key always ends in `.webp`
   and the content type is always `image/webp`. The returned `size` is the
   stored size.

Objects uploaded before this change are untouched. To get the smaller version
of an old photo, upload it again in the admin.

## Why on the server

- **One place, every client.** The admin app runs on the owners' phones and
  laptops. Resizing in the browser would be a courtesy the API can't rely on,
  the same argument decision 009 makes for validation.
- **The metadata has to go anyway.** Phone JPEGs carry GPS coordinates. Removing
  them must happen where it can't be skipped.
- **It happens once.** Two people, a few uploads a week. Paying the CPU at upload
  time costs nothing visible, and every page view after that gets the small file.

## Why WebP, 1600 and 82

- **WebP** is supported by every browser the site targets. At equal visual
  quality it is typically 25–35% smaller than JPEG, and it handles both photos
  and the odd graphic with transparency, so one output format covers all
  three inputs.
- **1600px** is the widest slot on the page (the hero, 1120px container) at
  about 1.5× density, which keeps the image sharp on most phone and laptop
  screens. A 3× phone screen at 375px wide needs about 1125px. Anything bigger
  is bytes nobody sees.
- **Quality 82** is about where lossy WebP stops showing artefacts on photos at
  normal viewing size. Above 85 the file grows quickly for little visible gain.
  It is a constant (`UploadService.WebpQuality`) so it is easy to tune.

## Why not Supabase image transformations

Supabase can resize on the fly through a render URL, but transformations are a
paid-plan feature and are billed per origin image. They would also leave the
4 MB original as the stored object and keep the GPS metadata in it. Doing it
once at upload is free and leaves nothing sensitive in the bucket.

## Why ImageSharp, and the licence

**ImageSharp over SkiaSharp** because of the API. ImageSharp is pure managed
code: `Identify`, `AutoOrient`, `Resize` and a `WebpEncoder` are one line each,
and nothing native has to be installed on the host. SkiaSharp is MIT-licensed
and fast, but it needs native Skia binaries for the deployment OS
(`SkiaSharp.NativeAssets.Linux` on most hosts), and EXIF orientation is left to
the caller. **SkiaSharp is the fallback** if the licence below ever stops
fitting.

**Licence: Six Labors Split License 1.0.** It is Apache 2.0 for open-source use
and for for-profit companies or individuals under USD 1M annual gross revenue,
and commercial above that. Klowee Cafe is far under the threshold, so the
Apache 2.0 terms apply. If the business ever passes it, the options are a
commercial licence or switching to SkiaSharp.

**Version 4.1.3, and the build-time key.** Since 4.x, the package runs a licence
check during the build. A Debug build without a key only warns. **A Release
build fails** (`No Six Labors license found`). The key is free for qualifying
users from sixlabors.com, and community keys are scoped to the assembly name
(`Klowee.Api`). It is passed to MSBuild as the `SixLaborsLicenseKey`
environment variable, or as a `sixlabors.lic` file, which is git-ignored. The
deploy pipeline must provide it.

The 3.x line needs no key, but every version up to 4.1.1 has published
advisories (GHSA-gwg2-r3hj-4w44, -j3p4-wp97-rph4, -j9gm-c75j-xc9q,
-jjfr-hcj7-qf5w, -wmxv-xphr-5c9g; three high). One is ICC parsing on the JPEG
decode path, which this API exposes to uploads. 4.1.2 is the first fixed
release, so 4.1.3 it is.

## Consequences

- Stored photos are typically 150–400 kB instead of several MB, and none of
  them carries location data.
- An upload takes a few hundred milliseconds of CPU and holds the decoded image
  in memory. At the 8000px cap that is at most about 256 MB per request for RGBA
  pixels. That's acceptable for two uploaders, but it is the number to watch on a
  small host.
- Animated WebP is flattened to its first frame.
- Release builds need `SixLaborsLicenseKey`. Forgetting it fails the build rather
  than the running app.
