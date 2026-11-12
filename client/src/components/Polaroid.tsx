import type { Ref } from 'react'
import type { PublicEvent } from '../api/types'
import { formatDateRange } from '../lib/format'
import { Sticker } from './Sticker'

/**
 * One event as a tilted polaroid. The whole card is a real `<button>`: it
 * toggles the panel under its row and says so with `aria-expanded`.
 */
export function Polaroid({
  event,
  index,
  badge,
  expanded,
  panelId,
  onToggle,
  buttonRef,
}: {
  event: PublicEvent
  /** Even cards lean left, odd ones right (by event, so an open panel doesn't flip the rest). */
  index: number
  badge: string | null
  expanded: boolean
  panelId: string
  onToggle: () => void
  buttonRef: Ref<HTMLButtonElement>
}) {
  const photos = event.photos.length

  return (
    <button
      ref={buttonRef}
      type="button"
      aria-expanded={expanded}
      aria-controls={expanded ? panelId : undefined}
      onClick={onToggle}
      className={`polaroid relative block w-full cursor-pointer rounded bg-panel px-2.5 pt-2.5 pb-4 text-left transition-transform duration-200 ${index % 2 ? 'rotate-[1.5deg]' : '-rotate-[1.5deg]'} hover:!rotate-0 focus-visible:!rotate-0 aria-expanded:!rotate-0`}
    >
      {badge && <Sticker className="absolute -top-3 -left-2 z-[1] px-[11px] text-[0.8rem]">{badge}</Sticker>}
      {event.coverPhotoUrl ? (
        <img
          src={event.coverPhotoUrl}
          alt=""
          loading="lazy"
          decoding="async"
          className="block aspect-[4/3] w-full bg-ph object-cover"
        />
      ) : (
        <span aria-hidden="true" className="grid aspect-[4/3] place-content-center bg-ph p-3 text-center text-[0.85rem] text-ph-ink">
          photo: {event.name}
        </span>
      )}
      {photos > 0 && (
        <span className="absolute top-[18px] right-[18px] rounded-full bg-panel px-2.5 py-0.5 text-[0.76rem] font-bold text-deep">
          {photos} {photos === 1 ? 'photo' : 'photos'}
        </span>
      )}
      <span className="mt-3 block font-hand text-[1.08rem] leading-[1.2] font-medium text-deep">{event.name}</span>
      <span className="mt-0.5 block text-[0.9rem] text-soft">{formatDateRange(event.startsOn, event.endsOn)}</span>
    </button>
  )
}
