import type { PublicEvent } from '../api/types'
import { paragraphs } from '../lib/content'
import { formatDateRange } from '../lib/format'

/** The open pop-up, under its row: name, dates and venue, the description, and a strip of its photos. */
export function EventPanel({ event, id, onClose }: { event: PublicEvent; id: string; onClose: () => void }) {
  return (
    <div id={id} role="region" aria-label={event.name} className="rounded-[22px] bg-panel px-[26px] pt-6 pb-[26px]">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h3 className="font-serif text-[1.4rem] leading-[1.15] text-deep">{event.name}</h3>
          <p className="mt-0.5 mb-2 text-[0.95rem] text-soft">
            {formatDateRange(event.startsOn, event.endsOn)}. {event.venue}.
          </p>
        </div>
        <button
          type="button"
          onClick={onClose}
          className="flex-none cursor-pointer rounded-full bg-ph px-3.5 py-1.5 text-[0.9rem] font-bold text-deep"
        >
          Close
        </button>
      </div>

      {paragraphs(event.description).map((paragraph, index) => (
        <p key={index} className="mt-2 max-w-[65ch]">
          {paragraph}
        </p>
      ))}

      {event.photos.length > 0 && (
        <ul className="mt-4 flex gap-3 overflow-x-auto pb-1 [scrollbar-width:thin]">
          {event.photos.map((photo, index) => (
            <li key={`${photo.url}-${index}`} className="flex-[0_0_min(240px,70%)]">
              <img
                src={photo.url}
                alt={photo.caption || `${event.name}, photo ${index + 1}`}
                loading="lazy"
                decoding="async"
                className="aspect-[4/3] w-full rounded-xl bg-ph object-cover"
              />
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
