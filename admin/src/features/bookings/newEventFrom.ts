import type { BookingDto } from '../../types/api'
import type { EventValues } from '../events/schemas'

/** Events allow 160 characters for a name and a venue (EventConfiguration); a request's venue may be 200. */
const EVENT_TEXT_MAX = 160

/**
 * What "Create event" puts in the New event form (handover 20): "{name}'s
 * event", the venue, the event date as both start and end, status Planned.
 * Passed as router state, not in the address, so a name and venue never end
 * up in a URL or a server log. The owner edits anything before saving.
 */
export function newEventFrom(booking: BookingDto): Partial<EventValues> {
  return {
    name: `${booking.name}'s event`.slice(0, EVENT_TEXT_MAX),
    venue: booking.venue.slice(0, EVENT_TEXT_MAX),
    startsOn: booking.eventDate,
    endsOn: booking.eventDate,
    status: 'Planned',
  }
}
