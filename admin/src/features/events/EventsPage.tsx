import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { useCreateEventMutation, useListEventsQuery, type EventFilters } from '../../api/eventsApi'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { EmptyState } from '../../components/ui/EmptyState'
import { SkeletonRows, Table, Td, Th } from '../../components/ui/Table'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import { formatDateRange } from '../../lib/format'
import { EVENT_STATUSES, type EventStatus } from '../../types/api'
import { EventForm } from './EventForm'
import { EVENT_STATUS_TONES } from './eventStatus'
import { toEventRequest } from './schemas'

const filterClasses = 'h-8 rounded-md border border-line bg-surface px-2 text-sm text-ink'

export function EventsPage() {
  const navigate = useNavigate()
  const [status, setStatus] = useState<EventStatus | ''>('')
  const [published, setPublished] = useState<'' | 'yes' | 'no'>('')

  const filters: EventFilters = {
    ...(status ? { status } : {}),
    ...(published ? { published: published === 'yes' } : {}),
  }
  const { data: events, isLoading, isFetching } = useListEventsQuery(filters)

  const [createEvent, { isLoading: isCreating }] = useCreateEventMutation()
  const run = useToastedAction()
  const [isFormOpen, setFormOpen] = useState(false)

  const isFiltered = status !== '' || published !== ''

  return (
    <>
      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center gap-4">
            <label className="flex items-center gap-2 text-sm text-muted">
              Status
              <select
                className={filterClasses}
                value={status}
                onChange={(event) => setStatus(event.target.value as EventStatus | '')}
              >
                <option value="">All</option>
                {EVENT_STATUSES.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </select>
            </label>
            <label className="flex items-center gap-2 text-sm text-muted">
              Published
              <select
                className={filterClasses}
                value={published}
                onChange={(event) => setPublished(event.target.value as '' | 'yes' | 'no')}
              >
                <option value="">All</option>
                <option value="yes">Published</option>
                <option value="no">Not published</option>
              </select>
            </label>
          </div>
          <Button variant="primary" size="sm" onClick={() => setFormOpen(true)}>
            New event
          </Button>
        </CardHeader>

        <Table>
          <thead>
            <tr>
              <Th className="w-[28%]">Name</Th>
              <Th>Venue</Th>
              <Th>Dates</Th>
              <Th>Status</Th>
              <Th>Site</Th>
              <Th className="text-right">Photos</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <SkeletonRows columns={6} />}
            {events?.map((event) => (
              <tr key={event.id} className={isFetching ? 'opacity-60' : undefined}>
                <Td>
                  <Link
                    to={`/site/events/${event.id}`}
                    className="font-medium hover:text-accent hover:underline"
                  >
                    {event.name}
                  </Link>
                </Td>
                <Td className="text-muted">{event.venue}</Td>
                <Td className="whitespace-nowrap text-muted">
                  {formatDateRange(event.startsOn, event.endsOn)}
                </Td>
                <Td>
                  <Badge tone={EVENT_STATUS_TONES[event.status]}>{event.status}</Badge>
                </Td>
                <Td>
                  <Badge tone={event.isPublished ? 'success' : 'neutral'}>
                    {event.isPublished ? 'Published' : 'Hidden'}
                  </Badge>
                </Td>
                <Td className="text-right text-muted">{event.photoCount}</Td>
              </tr>
            ))}
          </tbody>
        </Table>

        {!isLoading && (events?.length ?? 0) === 0 && (
          <EmptyState
            message={isFiltered ? 'No events match these filters.' : 'No events yet.'}
            action={
              !isFiltered && (
                <Button variant="primary" size="sm" onClick={() => setFormOpen(true)}>
                  New event
                </Button>
              )
            }
          />
        )}
      </Card>

      {isFormOpen && (
        <EventForm
          event={null}
          saving={isCreating}
          onClose={() => setFormOpen(false)}
          onSubmit={async (values) => {
            const created = await run(createEvent(toEventRequest(values)), 'Event created.')
            // Photos and publishing live on the detail page, so go straight there.
            if (created.ok) navigate(`/site/events/${created.data.id}`)
          }}
        />
      )}
    </>
  )
}
