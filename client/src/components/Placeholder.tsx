import { ASPECTS, type Aspect } from '../lib/aspects'

/**
 * An image slot with no image yet: a sky-light block at the right ratio with a
 * handwritten note naming what belongs there. Meant to look like a sketch of
 * the layout, not a failed load.
 */
export function Placeholder({
  caption,
  aspect,
  className = '',
}: {
  caption: string
  aspect: Aspect
  className?: string
}) {
  return (
    <div
      role="img"
      aria-label={`Placeholder: ${caption}`}
      className={`flex items-center justify-center rounded-[10px] bg-sky-light p-3 text-center ${ASPECTS[aspect]} ${className}`}
    >
      <span className="font-hand text-2xl leading-tight font-medium text-navy">{caption}</span>
    </div>
  )
}

/** Small square slots (menu thumbnails) have no room for a caption. */
export function PlaceholderThumb({ label }: { label: string }) {
  return (
    <div
      role="img"
      aria-label={`Placeholder: ${label}`}
      className="size-12 shrink-0 rounded-[10px] bg-sky-light"
    />
  )
}
