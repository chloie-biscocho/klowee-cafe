import { useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useDeleteBookingMutation, useGetBookingQuery, useUpdateBookingMutation } from '../../api/bookingsApi'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { Spinner } from '../../components/ui/Spinner'
import { Textarea } from '../../components/ui/Textarea'
import { formatManilaDateTime } from '../../lib/dates'
import { formatDate } from '../../lib/format'
import { BOOKING_STATUSES, type BookingDto, type BookingStatus } from '../../types/api'
import { useCopy } from '../toasts/useCopy'
import { useToastedAction } from '../toasts/useToastedAction'
import { BOOKING_STATUS_HINTS, BOOKING_STATUS_TONES } from './bookingStatus'
import { newEventFrom } from './newEventFrom'

/** One booking request: every field, the status, the owners' notes, and what to do next. */
export function BookingDetailPage() {
  const { id = '' } = useParams()
  const { data: booking, isLoading, isError } = useGetBookingQuery(id)

  if (isLoading) return <Spinner className="size-6 text-muted" />
  if (isError || !booking) {
    return (
      <Card>
        <EmptyState
          message="This booking request isn't here. It may have been deleted."
          action={
            <Link to="/site/bookings" className="text-accent underline">
              Back to booking requests
            </Link>
          }
        />
      </Card>
    )
  }
  // Keyed by the request, so moving to another one starts its notes afresh.
  return <BookingDetail key={booking.id} booking={booking} />
}

function BookingDetail({ booking }: { booking: BookingDto }) {
  const navigate = useNavigate()
  const run = useToastedAction()
  const copy = useCopy()
  const [update, { isLoading: isSaving }] = useUpdateBookingMutation()
  const [remove, { isLoading: isDeleting }] = useDeleteBookingMutation()
  const [notes, setNotes] = useState(booking.ownerNotes ?? '')
  const [savedNotes, setSavedNotes] = useState(booking.ownerNotes ?? '')
  const [busyStatus, setBusyStatus] = useState<BookingStatus | null>(null)
  const [confirmingDelete, setConfirmingDelete] = useState(false)

  // The API takes the status and the notes together; whatever is in the notes box goes too.
  const save = async (status: BookingStatus, message: string) => {
    const outcome = await run(update({ id: booking.id, body: { status, ownerNotes: notes.trim() === '' ? null : notes } }), message)
    if (outcome.ok) setSavedNotes(notes)
  }

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center gap-3">
            <h2 className="font-serif text-[1.5rem] leading-tight">{booking.name}</h2>
            <Badge tone={BOOKING_STATUS_TONES[booking.status]}>{booking.status}</Badge>
          </div>
          <div className="flex gap-2">
            <Button size="sm" variant="primary" onClick={() => navigate('/site/events', { state: { newEvent: newEventFrom(booking) } })}>
              Create event
            </Button>
            <Button size="sm" variant="danger" onClick={() => setConfirmingDelete(true)}>
              Delete
            </Button>
          </div>
        </CardHeader>

        <dl className="grid gap-x-6 gap-y-3 p-4 sm:grid-cols-[max-content_1fr]">
          <Row label="Event date">{formatDate(booking.eventDate)}</Row>
          <Row label="Contact">
            <span className="flex flex-wrap items-center gap-2">
              <span className="font-medium break-all">{booking.contact}</span>
              <Button size="sm" onClick={() => copy(booking.contact, 'Contact')}>
                Copy
              </Button>
            </span>
          </Row>
          <Row label="Venue">{booking.venue}</Row>
          <Row label="Guests">{booking.guestCount ?? 'Not said'}</Row>
          <Row label="Package">{booking.packageName ?? 'Not sure yet'}</Row>
          <Row label="Message">
            {booking.message ? <span className="whitespace-pre-line">{booking.message}</span> : <span className="text-muted">None</span>}
          </Row>
          <Row label="Sent">{formatManilaDateTime(booking.createdAt)}</Row>
        </dl>
      </Card>

      <Card>
        <CardHeader>
          <h2 className="text-[1.02rem] font-bold">Status</h2>
        </CardHeader>
        <div className="flex flex-col gap-4 p-4">
          <div role="group" aria-label="Status" className="flex flex-wrap gap-2">
            {BOOKING_STATUSES.map((status) => {
              const current = booking.status === status
              return (
                <Button
                  key={status}
                  size="sm"
                  variant={current ? 'primary' : 'secondary'}
                  aria-pressed={current}
                  title={BOOKING_STATUS_HINTS[status]}
                  loading={busyStatus === status}
                  // The current status stays solid (pressed), not greyed out; pressing it does nothing.
                  disabled={!current && busyStatus !== null}
                  onClick={async () => {
                    if (current) return
                    setBusyStatus(status)
                    await save(status, status === 'New' ? 'Back to New.' : `Marked ${status.toLowerCase()}.`)
                    setBusyStatus(null)
                  }}
                >
                  {status === 'New' && !current ? 'Back to New' : status}
                </Button>
              )
            })}
          </div>
          <p className="text-sm text-muted">
            {booking.statusChangedAt
              ? `Status changed ${formatManilaDateTime(booking.statusChangedAt)}${booking.statusChangedByName ? ` by ${booking.statusChangedByName}` : ''}.`
              : 'Status not changed yet.'}
          </p>

          <Textarea
            label="Your notes"
            hint="Only the two of you see these."
            rows={3}
            maxLength={1000}
            value={notes}
            onChange={(event) => setNotes(event.target.value)}
          />
          <div>
            <Button size="sm" loading={isSaving && busyStatus === null} disabled={notes === savedNotes || busyStatus !== null} onClick={() => save(booking.status, 'Notes saved.')}>
              Save notes
            </Button>
          </div>
        </div>
      </Card>

      <ConfirmDialog
        open={confirmingDelete}
        title="Delete booking request"
        message={`Delete ${booking.name}'s request? It disappears from Bookings. An event made from it stays.`}
        loading={isDeleting}
        onCancel={() => setConfirmingDelete(false)}
        onConfirm={async () => {
          const outcome = await run(remove(booking.id), 'Booking request deleted.')
          setConfirmingDelete(false)
          if (outcome.ok) navigate('/site/bookings')
        }}
      />
    </div>
  )
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-sm font-bold text-muted">{label}</dt>
      <dd>{children}</dd>
    </>
  )
}
