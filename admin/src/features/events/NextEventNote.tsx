import { useListEventsQuery } from '../../api/eventsApi'
import { todayInManila } from '../../lib/dates'
import type { EventDetailDto } from '../../types/api'
import { nextEventVerdict } from './eventStatus'

/** "Preview on site": whether the home page would show this as the next event. */
export function NextEventNote({ event }: { event: EventDetailDto }) {
  // The same cache entry the list page uses when filtered this way, and it is
  // refreshed by the same tags, so publishing updates this note by itself.
  const { data: candidates } = useListEventsQuery({ status: 'Upcoming', published: true })
  const verdict = nextEventVerdict(event, candidates ?? [], todayInManila())

  const text =
    verdict.kind === 'next'
      ? 'This is the next event on the public site.'
      : verdict.kind === 'behind'
        ? `Eligible, but "${verdict.other.name}" starts first, so the site shows that one as next.`
        : `Not shown as the next event: ${verdict.reason}.`

  return (
    <p
      className={`mb-4 rounded-md border px-3 py-2 text-sm ${
        verdict.kind === 'next'
          ? 'border-success/30 bg-success-soft text-success'
          : 'border-line bg-surface text-muted'
      }`}
    >
      <span className="font-semibold">Preview on site.</span> {text}
    </p>
  )
}
