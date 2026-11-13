import { useState } from 'react'
import { Link } from 'react-router'
import { useGetLoyaltyQuery } from '../../../api/salesApi'
import { clubLabel } from '../../../lib/loyalty'
import type { CustomerClubDto } from '../../../types/api'
import { EmptyState } from '../../../components/ui/EmptyState'
import { SkeletonRows, Table, Td, Th } from '../../../components/ui/Table'
import { formatManilaDateTime } from '../../../lib/dates'

/** The stamp ledger, newest first, for every card or one. */
export function LoyaltyLedger({ customerId, clubs }: { customerId: string; clubs: CustomerClubDto[] }) {
  const [clubId, setClubId] = useState('')
  const { data: entries, isLoading } = useGetLoyaltyQuery({ id: customerId, clubId: clubId || null })

  const filter = (
    <label className="flex items-center gap-2 px-3 pt-3 text-sm">
      <span className="text-muted">Card</span>
      <select aria-label="Card" className="h-8 rounded-md border border-line px-2 text-sm" value={clubId} onChange={(e) => setClubId(e.target.value)}>
        <option value="">All cards</option>
        {clubs.map((club) => (
          <option key={club.clubId} value={club.clubId}>
            {club.name}
          </option>
        ))}
      </select>
    </label>
  )

  if (!isLoading && (entries?.length ?? 0) === 0) {
    return (
      <>
        {filter}
        <EmptyState message="No stamps yet. Paid drinks of a card's categories earn one each." />
      </>
    )
  }

  return (
    <>
    {filter}
    <Table>
      <thead>
        <tr>
          <Th>When</Th>
          <Th>Card</Th>
          <Th className="text-right">Stamps</Th>
          <Th>Reason</Th>
          <Th>Order</Th>
          <Th>Notes</Th>
        </tr>
      </thead>
      <tbody>
        {isLoading && <SkeletonRows columns={6} />}
        {entries?.map((entry) => (
          <tr key={entry.id}>
            <Td className="whitespace-nowrap text-muted">{formatManilaDateTime(entry.createdAt)}</Td>
            <Td>{clubLabel(entry.clubName)}</Td>
            <Td className={`text-right font-semibold ${entry.delta < 0 ? 'text-danger' : 'text-success'}`}>
              {entry.delta > 0 ? `+${entry.delta}` : entry.delta}
            </Td>
            <Td>{entry.reason}</Td>
            <Td>
              {entry.orderId ? (
                <Link className="text-accent hover:underline" to={`/sales/orders/${entry.orderId}`}>
                  View
                </Link>
              ) : (
                '—'
              )}
            </Td>
            <Td className="text-muted">{entry.notes ?? ''}</Td>
          </tr>
        ))}
      </tbody>
    </Table>
    </>
  )
}
