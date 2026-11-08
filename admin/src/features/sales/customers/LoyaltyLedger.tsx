import { Link } from 'react-router'
import { useGetLoyaltyQuery } from '../../../api/salesApi'
import { EmptyState } from '../../../components/ui/EmptyState'
import { SkeletonRows, Table, Td, Th } from '../../../components/ui/Table'
import { formatManilaDateTime } from '../../../lib/dates'

export function LoyaltyLedger({ customerId }: { customerId: string }) {
  const { data: entries, isLoading } = useGetLoyaltyQuery(customerId)
  if (!isLoading && (entries?.length ?? 0) === 0) {
    return <EmptyState message="No stamps yet. Paid drinks earn one each." />
  }

  return (
    <Table>
      <thead>
        <tr>
          <Th>When</Th>
          <Th className="text-right">Stamps</Th>
          <Th>Reason</Th>
          <Th>Order</Th>
          <Th>Notes</Th>
        </tr>
      </thead>
      <tbody>
        {isLoading && <SkeletonRows columns={5} />}
        {entries?.map((entry) => (
          <tr key={entry.id}>
            <Td className="whitespace-nowrap text-muted">{formatManilaDateTime(entry.createdAt)}</Td>
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
  )
}
