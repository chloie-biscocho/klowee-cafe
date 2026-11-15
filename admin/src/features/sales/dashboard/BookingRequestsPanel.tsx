import { Link } from 'react-router'
import { useListBookingsQuery } from '../../../api/bookingsApi'
import { Card, CardHeader } from '../../../components/ui/Card'
import { Table, Td, Th } from '../../../components/ui/Table'
import { formatDate } from '../../../lib/format'

const SHOWN = 5

/**
 * Booking requests nobody has answered yet (handover 20), like Payments to
 * confirm. Hidden when none are New.
 */
export function BookingRequestsPanel() {
  const { data } = useListBookingsQuery({ status: 'New', page: 1, pageSize: SHOWN })

  if (!data || data.totalCount === 0) return null

  return (
    <Card>
      <CardHeader>
        <h2 className="text-[1.02rem] font-bold">Booking requests</h2>
        {data.totalCount > SHOWN && (
          <Link to="/site/bookings" className="text-sm text-accent hover:underline">
            See all {data.totalCount}
          </Link>
        )}
      </CardHeader>
      <Table>
        <thead>
          <tr>
            <Th>Event date</Th>
            <Th>Name</Th>
            <Th>Venue</Th>
            <Th>
              <span className="sr-only">Actions</span>
            </Th>
          </tr>
        </thead>
        <tbody>
          {data.items.map((booking) => (
            <tr key={booking.id}>
              <Td className="whitespace-nowrap">{formatDate(booking.eventDate)}</Td>
              <Td>{booking.name}</Td>
              <Td className="text-muted">{booking.venue}</Td>
              <Td className="text-right">
                <Link
                  to={`/site/bookings/${booking.id}`}
                  className="text-sm font-bold text-accent hover:underline"
                  aria-label={`Open ${booking.name}'s request`}
                >
                  Open
                </Link>
              </Td>
            </tr>
          ))}
        </tbody>
      </Table>
    </Card>
  )
}
