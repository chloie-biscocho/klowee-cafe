import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useDeleteOrderMutation, useGetOrderQuery } from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { Card, CardHeader } from '../../../components/ui/Card'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import { EmptyState } from '../../../components/ui/EmptyState'
import { Spinner } from '../../../components/ui/Spinner'
import { Table, Td, Th } from '../../../components/ui/Table'
import { formatDate, formatPeso } from '../../../lib/format'
import { useToastedAction } from '../../toasts/useToastedAction'
import { StatusBadge } from '../StatusBadge'
import { PaymentsSection } from './PaymentsSection'

const DISCOUNT_LABEL = { None: '', Percent: '% off', Fixed: 'off', LoyaltyFree: 'Free drink (old card)' } as const

export function OrderDetailPage() {
  const { id = '' } = useParams()
  const { data: order, isLoading, isError } = useGetOrderQuery(id)
  const [deleteOrder, { isLoading: isDeleting }] = useDeleteOrderMutation()
  const run = useToastedAction()
  const navigate = useNavigate()
  const [confirming, setConfirming] = useState(false)

  if (isLoading) return <Spinner className="size-6 text-muted" />
  if (isError || !order) return <EmptyState message="This order doesn't exist, or was deleted." />

  const locked = order.payments.length > 0
  const discount =
    order.discountType === 'Percent'
      ? `${order.discountValue}${DISCOUNT_LABEL.Percent}`
      : order.discountType === 'Fixed'
        ? `${formatPeso(order.discountValue)} ${DISCOUNT_LABEL.Fixed}`
        : DISCOUNT_LABEL[order.discountType]

  return (
    <div className="flex flex-col gap-4">
      <Card className="flex flex-wrap items-start justify-between gap-6 p-5">
        <div className="flex flex-col gap-1">
          <p className="text-sm text-muted">{formatDate(order.orderedOn)}</p>
          <h2 className="text-lg font-bold">
            <Link className="hover:underline" to={`/sales/customers/${order.customer.id}`}>
              {order.customer.name}
            </Link>
            {order.payer && (
              <span className="ml-2 text-sm font-normal text-muted">
                c/o{' '}
                <Link className="hover:underline" to={`/sales/customers/${order.payer.id}`}>
                  {order.payer.name}
                </Link>
              </span>
            )}
          </h2>
          <div>
            <StatusBadge status={order.status} />
          </div>
          {order.notes && <p className="mt-1 max-w-prose text-sm">{order.notes}</p>}
        </div>
        <dl className="grid grid-cols-3 gap-6 text-right">
          {[
            ['Total', order.total],
            ['Paid', order.paid],
            ['Balance', order.balance],
          ].map(([label, amount]) => (
            <div key={label}>
              <dt className="text-xs font-semibold text-muted uppercase">{label}</dt>
              <dd className={`text-xl font-bold ${label === 'Balance' && Number(amount) > 0 ? 'text-danger' : ''}`}>
                {formatPeso(Number(amount))}
              </dd>
            </div>
          ))}
        </dl>
        <div className="flex gap-2">
          <span title={locked ? 'This order has payments. Remove them to change its drinks or discount.' : undefined}>
            <Button disabled={locked} onClick={() => navigate(`/sales/orders/${order.id}/edit`)}>
              Edit
            </Button>
          </span>
          <Button variant="danger" onClick={() => setConfirming(true)}>
            Delete
          </Button>
        </div>
      </Card>

      <Card>
        <CardHeader>
          <h2 className="text-sm font-semibold">Drinks</h2>
          {discount && <span className="text-xs text-muted">Discount: {discount}</span>}
        </CardHeader>
        <Table>
          <thead>
            <tr>
              <Th>Drink</Th>
              <Th className="text-right">Qty</Th>
              <Th className="text-right">Price</Th>
              <Th className="text-right">Line</Th>
            </tr>
          </thead>
          <tbody>
            {order.items.map((item) => (
              <tr key={item.id}>
                <Td>
                  {item.name}
                  {item.addOns.map((addOn) => (
                    <span key={addOn.id} className="block text-xs text-muted">
                      + {addOn.name} {formatPeso(addOn.unitPrice)}
                    </span>
                  ))}
                  {item.reward && (
                    <span className="block text-xs text-success">
                      ★ {item.reward.label}
                      {item.reward.amountOff > 0 ? ` −${formatPeso(item.reward.amountOff)}` : ' (given free)'}
                    </span>
                  )}
                </Td>
                <Td className="text-right">{item.quantity}</Td>
                <Td className="text-right">{formatPeso(item.unitPrice)}</Td>
                <Td className="text-right">{formatPeso(item.lineTotal)}</Td>
              </tr>
            ))}
          </tbody>
        </Table>
        <div className="flex flex-col items-end gap-0.5 px-4 py-3 text-sm">
          <span className="text-muted">Subtotal {formatPeso(order.subtotal)}</span>
          {order.discountAmount > 0 && <span className="text-success">Discount −{formatPeso(order.discountAmount)}</span>}
          <span className="font-semibold">Total {formatPeso(order.total)}</span>
        </div>
        {order.warnings.map((warning) => (
          <p key={warning} className="border-t border-line px-4 py-2 text-xs text-muted">
            ⚠ {warning}
          </p>
        ))}
      </Card>

      <PaymentsSection key={`${order.payments.length}-${order.balance}`} order={order} />

      <ConfirmDialog
        open={confirming}
        title="Delete order"
        message={`Delete this order for ${order.customer.name}? Its payments are removed with it, and any stamps it earned or used are reversed.`}
        loading={isDeleting}
        onCancel={() => setConfirming(false)}
        onConfirm={async () => {
          const outcome = await run(deleteOrder(order.id), 'Order deleted.')
          setConfirming(false)
          if (outcome.ok) navigate('/sales/orders', { replace: true })
        }}
      />
    </div>
  )
}
