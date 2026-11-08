import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useAddPaymentMutation, useDeletePaymentMutation } from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { Card, CardHeader } from '../../../components/ui/Card'
import { Table, Td, Th } from '../../../components/ui/Table'
import { todayInManila } from '../../../lib/dates'
import { formatDate, formatPeso } from '../../../lib/format'
import { PAYMENT_CHANNEL_LABELS, type OrderDetailDto } from '../../../types/api'
import { useToastedAction } from '../../toasts/useToastedAction'
import { PaymentFields } from '../PaymentFields'
import { blankToNull, paymentSchema, type PaymentValues } from '../schemas'

/**
 * Payments on one order, and the inline form to take another. The amount
 * defaults to what's still owed; more than that is the API's 409, shown as a
 * toast like every other refusal.
 */
export function PaymentsSection({ order }: { order: OrderDetailDto }) {
  const [addPayment, { isLoading: isAdding }] = useAddPaymentMutation()
  const [deletePayment] = useDeletePaymentMutation()
  const run = useToastedAction()
  const { register, handleSubmit, formState, reset } = useForm<PaymentValues>({
    resolver: zodResolver(paymentSchema),
    defaultValues: { amount: order.balance, channel: 'GCash', paidOn: todayInManila(), reference: '' },
  })

  const submit = handleSubmit(async (values) => {
    const outcome = await run(
      addPayment({ orderId: order.id, body: { ...values, reference: blankToNull(values.reference), notes: null } }),
      `Payment of ${formatPeso(values.amount)} recorded.`,
    )
    if (outcome.ok) {
      reset({ amount: outcome.data.balance, channel: values.channel, paidOn: values.paidOn, reference: '' })
    }
  })

  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold">Payments</h2>
      </CardHeader>
      {order.payments.length > 0 && (
        <Table>
          <thead>
            <tr>
              <Th>Date</Th>
              <Th>Channel</Th>
              <Th className="text-right">Amount</Th>
              <Th>Reference</Th>
              <Th className="text-right" />
            </tr>
          </thead>
          <tbody>
            {order.payments.map((payment) => (
              <tr key={payment.id}>
                <Td>{formatDate(payment.paidOn)}</Td>
                <Td>{PAYMENT_CHANNEL_LABELS[payment.channel]}</Td>
                <Td className="text-right">{formatPeso(payment.amount)}</Td>
                <Td className="text-muted">{payment.reference ?? ''}</Td>
                <Td className="text-right">
                  <Button
                    size="sm"
                    variant="ghost"
                    aria-label={`Delete payment of ${formatPeso(payment.amount)}`}
                    onClick={() => run(deletePayment({ orderId: order.id, paymentId: payment.id }), 'Payment removed.')}
                  >
                    Delete
                  </Button>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
      {order.balance > 0 ? (
        <form className="flex flex-col gap-3 p-4" onSubmit={submit} aria-label="Add payment">
          <PaymentFields register={register} errors={formState.errors} />
          <Button type="submit" variant="primary" size="sm" className="self-end" loading={isAdding}>
            Add payment
          </Button>
        </form>
      ) : (
        <p className="px-4 py-3 text-sm text-success">Paid in full.</p>
      )}
    </Card>
  )
}
