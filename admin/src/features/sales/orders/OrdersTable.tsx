import { useNavigate } from 'react-router'
import { SkeletonRows, Table, Td, Th } from '../../../components/ui/Table'
import { formatDate, formatPeso } from '../../../lib/format'
import type { OrderSummaryDto } from '../../../types/api'
import { StatusBadge } from '../StatusBadge'

/** The orders list and a customer's recent orders: one row shape, click to open. */
export function OrdersTable({ orders, loading }: { orders: OrderSummaryDto[]; loading?: boolean }) {
  const navigate = useNavigate()
  return (
    <Table>
      <thead>
        <tr>
          <Th>Date</Th>
          <Th>Customer</Th>
          <Th>Items</Th>
          <Th className="text-right">Total</Th>
          <Th className="text-right">Paid</Th>
          <Th>Status</Th>
        </tr>
      </thead>
      <tbody>
        {loading && <SkeletonRows columns={6} />}
        {orders.map((order) => (
          <tr key={order.id} className="cursor-pointer hover:bg-canvas" onClick={() => navigate(`/sales/orders/${order.id}`)}>
            <Td className="whitespace-nowrap">{formatDate(order.orderedOn)}</Td>
            <Td>
              {order.customer.name}
              {order.payer && <span className="ml-1.5 text-xs text-muted">c/o {order.payer.name}</span>}
            </Td>
            <Td className="text-muted">{order.items}</Td>
            <Td className="text-right">{formatPeso(order.total)}</Td>
            <Td className="text-right text-muted">{formatPeso(order.paid)}</Td>
            <Td>
              <StatusBadge status={order.status} />
            </Td>
          </tr>
        ))}
      </tbody>
    </Table>
  )
}
