import type { Ref } from 'react'
import type { PublicEvent } from '../api/types'
import { formatDateRange } from '../lib/format'
import { Photo } from './Photo'
import { Sticker } from './Sticker'

/**
 * One event as a polaroid. The whole card is a real `<button>`: it toggles the
 * panel below the row, says so through `aria-expanded`, and is reachable and
 * operable from the keyboard for free.
 */
export function Polaroid({
  event,
  index,
  isNext,
  expanded,
  panelId,
  onToggle,
  buttonRef,
}: {
  event: PublicEvent
  index: number
  isNext: boolean
  expanded: boolean
  panelId: string
  onToggle: () => void
  buttonRef: Ref<HTMLButtonElement>
}) {
  const caption = `${event.name} · ${formatDateRange(event.startsOn, event.endsOn)}`

  return (
    <button
      ref={buttonRef}
      type="button"
      aria-expanded={expanded}
      aria-controls={expanded ? panelId : undefined}
      onClick={onToggle}
      className={`group relative block w-full bg-white p-3 pb-4 text-left ${
        index % 2 === 0 ? '-rotate-2' : 'rotate-2'
      } ${expanded ? 'outline-2 outline-cobalt' : ''}`}
    >
      {isNext && (
        <Sticker className="absolute -top-3 -right-2 z-10" tilt="right">
          next
        </Sticker>
      )}
      <Photo
        url={event.coverPhotoUrl}
        alt={event.name}
        placeholder={`photo of the cart at ${event.venue}`}
        aspect="polaroid"
      />
      <span className="mt-3 block font-hand text-2xl leading-tight font-semibold text-navy group-hover:text-cobalt">
        {caption}
      </span>
    </button>
  )
}
