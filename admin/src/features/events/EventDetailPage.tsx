import { useState } from 'react'
import { Navigate, useNavigate, useParams } from 'react-router'
import {
  useDeleteEventMutation,
  useGetEventQuery,
  useSetEventPublishedMutation,
  useUpdateEventMutation,
} from '../../api/eventsApi'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { Spinner } from '../../components/ui/Spinner'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import { EventDetailHeader } from './EventDetailHeader'
import { EventForm } from './EventForm'
import { EventPhotosEditor } from './EventPhotosEditor'
import { NextEventNote } from './NextEventNote'
import { toEventRequest } from './schemas'

export function EventDetailPage() {
  const { id } = useParams<'id'>()
  if (!id) return <Navigate to="/site/events" replace />

  // Remounting per event is what discards a half-finished photo edit of another one.
  return <EventDetail key={id} eventId={id} />
}

function EventDetail({ eventId }: { eventId: string }) {
  const navigate = useNavigate()
  const run = useToastedAction()

  const { data: event, isLoading } = useGetEventQuery(eventId)
  const [updateEvent, { isLoading: isUpdating }] = useUpdateEventMutation()
  const [setPublished, { isLoading: isPublishing }] = useSetEventPublishedMutation()
  const [deleteEvent, { isLoading: isDeleting }] = useDeleteEventMutation()

  const [isFormOpen, setFormOpen] = useState(false)
  const [isDeleteOpen, setDeleteOpen] = useState(false)

  if (isLoading) return <Spinner className="size-6 text-muted" />
  if (!event) return <EmptyState message="That event no longer exists." />

  return (
    <>
      <EventDetailHeader
        event={event}
        isPublishing={isPublishing}
        onEdit={() => setFormOpen(true)}
        onDelete={() => setDeleteOpen(true)}
        onTogglePublish={() =>
          run(
            setPublished({ id: eventId, isPublished: !event.isPublished }),
            event.isPublished ? 'Event unpublished.' : 'Event published.',
          )
        }
      />

      <NextEventNote event={event} />

      <EventPhotosEditor eventId={eventId} photos={event.photos} />

      {isFormOpen && (
        <EventForm
          event={event}
          saving={isUpdating}
          onClose={() => setFormOpen(false)}
          onSubmit={async (values) => {
            const saved = await run(
              updateEvent({ id: eventId, body: toEventRequest(values) }),
              'Event updated.',
            )
            if (saved.ok) setFormOpen(false)
          }}
        />
      )}

      <ConfirmDialog
        open={isDeleteOpen}
        title="Delete event"
        message={`Delete "${event.name}" and its photo list? It disappears from the public site.`}
        loading={isDeleting}
        onCancel={() => setDeleteOpen(false)}
        onConfirm={async () => {
          const deleted = await run(deleteEvent(eventId), 'Event deleted.')
          if (deleted.ok) navigate('/site/events', { replace: true })
        }}
      />
    </>
  )
}
