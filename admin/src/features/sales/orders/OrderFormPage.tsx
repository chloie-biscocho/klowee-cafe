import { Link, useParams } from 'react-router'
import { useListAddOnsQuery } from '../../../api/menuApi'
import { useGetCurrentOfficeMenuQuery, useGetOrderQuery, useListCustomersQuery } from '../../../api/salesApi'
import { Card } from '../../../components/ui/Card'
import { EmptyState } from '../../../components/ui/EmptyState'
import { Spinner } from '../../../components/ui/Spinner'
import { todayInManila } from '../../../lib/dates'
import { draftFromOrder, emptyDraft, pickerItems } from './orderDraft'
import { OrderForm } from './OrderForm'

/**
 * /sales/orders/new and /sales/orders/:id/edit. Orders are taken from the
 * current office menu, so without one there is nothing to sell from
 * (decision 017); an order with payments is shown, not edited.
 */
export function OrderFormPage() {
  const { id } = useParams()
  const menu = useGetCurrentOfficeMenuQuery()
  const order = useGetOrderQuery(id ?? '', { skip: !id })
  const { data: addOns } = useListAddOnsQuery()
  const { data: customers } = useListCustomersQuery({ includeInactive: true })

  if (menu.isLoading || order.isLoading || !addOns || !customers) {
    return <Spinner className="size-6 text-muted" />
  }

  if (menu.isError) {
    return (
      <Card>
        <EmptyState
          message="Publish an office menu version first. New orders are taken from the current office menu, and there isn't one yet."
          action={
            <Link className="text-sm font-semibold text-accent hover:underline" to="/menu/versions">
              Go to menu versions
            </Link>
          }
        />
      </Card>
    )
  }

  if (id && !order.data) return <EmptyState message="This order doesn't exist, or was deleted." />

  if (order.data && order.data.payments.length > 0) {
    return (
      <Card>
        <EmptyState
          message="This order has payments, so its drinks and discount are locked: they're what was paid against. Remove the payments on the order page to edit it."
          action={
            <Link className="text-sm font-semibold text-accent hover:underline" to={`/sales/orders/${order.data.id}`}>
              Back to the order
            </Link>
          }
        />
      </Card>
    )
  }

  return (
    <OrderForm
      key={order.data?.id ?? 'new'}
      initial={order.data ? draftFromOrder(order.data) : emptyDraft(todayInManila())}
      order={order.data}
      items={pickerItems(menu.data, order.data)}
      addOns={addOns}
      customers={customers}
    />
  )
}
