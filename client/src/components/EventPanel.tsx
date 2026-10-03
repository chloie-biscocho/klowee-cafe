import type { PublicEvent } from '../api/types'
import { paragraphs } from '../lib/content'
import { formatDateRange } from '../lib/format'
import { PaperCard } from './PaperCard'

/** The expanded view under a polaroid's row: description and the photo strip. */
export function EventPanel({
  event,
  id,
  onClose,
}: {
  event: PublicEvent
  id: string
  onClose: () => void
}) {
  const description = paragraphs(event.description)

  return (
    <PaperCard as="div" className="col-span-full px-5 py-6 md:px-8">
      <div id={id} role="region" aria-label={event.name}>
        <div className="flex items-start justify-between gap-4">
          <div>
            <h3 className="font-heading text-3xl leading-[1.05] text-navy">{event.name}</h3>
            <p className="mt-1 text-navy">
              {event.venue} · {formatDateRange(event.startsOn, event.endsOn)}
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="flex size-11 shrink-0 items-center justify-center rounded-full text-2xl text-navy hover:bg-sky-light"
          >
            <span aria-hidden="true">×</span>
          </button>
        </div>

        {description.map((paragraph, index) => (
          <p key={index} className="mt-3 max-w-prose text-navy">
            {paragraph}
          </p>
        ))}

        {event.photos.length > 0 && (
          // A swipeable strip on phones; wraps into rows on wider screens.
          <ul className="-mx-5 mt-6 flex snap-x gap-4 overflow-x-auto px-5 pb-2 md:mx-0 md:flex-wrap md:overflow-visible md:px-0">
            {event.photos.map((photo, index) => (
              <li key={`${photo.url}-${index}`} className="w-56 shrink-0 snap-start bg-white p-2 pb-3">
                <img
                  src={photo.url}
                  alt={photo.caption || `${event.name}, photo ${index + 1}`}
                  loading="lazy"
                  decoding="async"
                  className="aspect-square w-full bg-sky-light object-cover"
                />
                {photo.caption && (
                  <p className="mt-2 font-hand text-xl leading-tight font-medium text-navy">
                    {photo.caption}
                  </p>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
    </PaperCard>
  )
}
