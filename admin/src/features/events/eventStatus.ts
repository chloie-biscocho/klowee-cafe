import type { EventStatus, EventSummaryDto } from '../../types/api'

export const EVENT_STATUS_TONES = {
  Planned: 'neutral',
  Upcoming: 'accent',
  Done: 'success',
  Cancelled: 'danger',
} as const satisfies Record<EventStatus, string>

/** The API's publish rule (`EventService.SetPublishedAsync`), mirrored for the button. */
export function canPublish(status: EventStatus): boolean {
  return status === 'Upcoming' || status === 'Done'
}

export const PUBLISH_BLOCKED_HINT =
  "Planned and Cancelled events can't be published. Set the status to Upcoming or Done first."

type NextEventVerdict =
  | { kind: 'next' }
  | { kind: 'behind'; other: EventSummaryDto }
  | { kind: 'excluded'; reason: string }

/**
 * Would the public site show this event as "next"? The same rule as
 * `EventService.GetNextAsync`: published, Upcoming, and ending today or later
 * in Manila; among those, the earliest start wins. Feedback only — the API
 * decides what the site actually shows.
 */
export function nextEventVerdict(
  event: Pick<EventSummaryDto, 'id' | 'status' | 'isPublished' | 'endsOn'>,
  others: EventSummaryDto[],
  today: string,
): NextEventVerdict {
  if (!event.isPublished) return { kind: 'excluded', reason: "it isn't published" }
  if (event.status !== 'Upcoming') {
    return { kind: 'excluded', reason: `its status is ${event.status}, not Upcoming` }
  }
  // ISO dates compare correctly as strings.
  if (event.endsOn < today) return { kind: 'excluded', reason: 'it has already ended' }

  const first = others
    .filter(
      (other) =>
        other.isPublished && other.status === 'Upcoming' && other.endsOn >= today,
    )
    .sort((a, b) => a.startsOn.localeCompare(b.startsOn))[0]

  return !first || first.id === event.id ? { kind: 'next' } : { kind: 'behind', other: first }
}
