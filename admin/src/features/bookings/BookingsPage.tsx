import { useState } from 'react'
import { Link } from 'react-router'
import { useListBookingsQuery } from '../../api/bookingsApi'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { EmptyState } from '../../components/ui/EmptyState'
import { SkeletonRows, Table, Td, Th } from '../../components/ui/Table'
import { formatDate } from '../../lib/format'
import { BOOKING_STATUSES, type BookingStatus } from '../../types/api'
import { BOOKING_STATUS_TONES } from './bookingStatus'

const PAGE_SIZE = 20
const filterClasses = 'h-8 rounded-full border-[1.5px] border-line bg-surface px-2 text-sm text-ink'

/**
 * Site → Bookings (handover 20): requests sent from the homepage, newest
 * first. Opens on New, the ones nobody has answered yet.
 */
export function BookingsPage() {
  const [status, setStatus] = useState<BookingStatus | ''>('New')
  const [page, setPage] = useState(1)
  const { data, isLoading, isFetching } = useListBookingsQuery({ ...(status ? { status } : {}), page, pageSize: PAGE_SIZE })
  const pages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / PAGE_SIZE))

  return (
    <Card>
      <CardHeader>
        <label className="flex items-center gap-2 text-sm text-muted">
          Status
          <select
            className={filterClasses}
            value={status}
            onChange={(event) => {
              setStatus(event.target.value as BookingStatus | '')
              setPage(1)
            }}
          >
            {BOOKING_STATUSES.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
            <option value="">All</option>
          </select>
        </label>
      </CardHeader>

      <Table>
        <thead>
          <tr>
            <Th>Event date</Th>
            <Th>Name</Th>
            <Th>Venue</Th>
            <Th className="text-right">Guests</Th>
            <Th>Package</Th>
            <Th>Status</Th>
          </tr>
        </thead>
        <tbody>
          {isLoading && <SkeletonRows columns={6} />}
          {data?.items.map((booking) => (
            <tr key={booking.id} className={isFetching ? 'opacity-60' : undefined}>
              <Td className="whitespace-nowrap">{formatDate(booking.eventDate)}</Td>
              <Td>
                <Link to={`/site/bookings/${booking.id}`} className="font-medium hover:text-accent hover:underline">
                  {booking.name}
                </Link>
              </Td>
              <Td className="text-muted">{booking.venue}</Td>
              <Td className="text-right text-muted">{booking.guestCount ?? '–'}</Td>
              <Td className="text-muted">{booking.packageName ?? 'Not sure yet'}</Td>
              <Td>
                <Badge tone={BOOKING_STATUS_TONES[booking.status]}>{booking.status}</Badge>
              </Td>
            </tr>
          ))}
        </tbody>
      </Table>

      {!isLoading && (data?.items.length ?? 0) === 0 && (
        <EmptyState message={status ? `No ${status.toLowerCase()} booking requests.` : 'No booking requests yet.'} />
      )}

      {pages > 1 && (
        <div className="flex items-center justify-end gap-3 px-4 py-3 text-sm">
          <Button size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
            Previous
          </Button>
          <span className="text-muted">
            Page {page} of {pages}
          </span>
          <Button size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>
            Next
          </Button>
        </div>
      )}
    </Card>
  )
}
