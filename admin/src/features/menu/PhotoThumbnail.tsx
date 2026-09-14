/**
 * A 40px square for the item photo. Uploading is not built yet, so an item
 * usually has no URL; the placeholder keeps the column from collapsing and
 * makes "no photo" obvious at a glance.
 */
export function PhotoThumbnail({ url, name }: { url: string | null; name: string }) {
  if (!url) {
    return (
      <div
        aria-label="No photo"
        className="flex size-10 items-center justify-center rounded border border-dashed border-line text-[10px] text-muted"
      >
        —
      </div>
    )
  }

  return (
    <img
      src={url}
      alt={name}
      loading="lazy"
      className="size-10 rounded border border-line object-cover"
    />
  )
}
