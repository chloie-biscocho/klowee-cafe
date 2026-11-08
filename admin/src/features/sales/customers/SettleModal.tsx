import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router'
import { useSettleTabMutation } from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { formatDate, formatPeso } from '../../../lib/format'
import { todayInManila } from '../../../lib/dates'
import { useToastedAction } from '../../toasts/useToastedAction'
import type { PaymentDto } from '../../../types/api'
import { PaymentFields } from '../PaymentFields'
import { blankToNull, paymentSchema, type PaymentValues } from '../schemas'

/**
 * "Paid 500 for last month": the API spreads the amount over the tab, oldest
 * order first. On success the modal shows the payments it made, so the owner
 * can see exactly which orders the money went to.
 */
export function SettleModal({
  customer,
  onClose,
}: {
  customer: { id: string; name: string; balance: number }
  onClose: () => void
}) {
  const [settle, { isLoading }] = useSettleTabMutation()
  const run = useToastedAction()
  const [result, setResult] = useState<PaymentDto[] | null>(null)
  const { register, handleSubmit, formState } = useForm<PaymentValues>({
    resolver: zodResolver(paymentSchema),
    defaultValues: { amount: customer.balance, channel: 'GCash', paidOn: todayInManila(), reference: '' },
  })

  const submit = handleSubmit(async (values) => {
    const outcome = await run(
      settle({ id: customer.id, body: { ...values, reference: blankToNull(values.reference), notes: null } }),
      `Recorded ${formatPeso(values.amount)} from ${customer.name}.`,
    )
    if (outcome.ok) setResult(outcome.data)
  })

  return (
    <Modal
      open
      title={`Settle ${customer.name}'s tab`}
      onClose={onClose}
      width="max-w-2xl"
      footer={
        result ? (
          <Button variant="primary" onClick={onClose}>
            Done
          </Button>
        ) : (
          <>
            <Button onClick={onClose} disabled={isLoading}>
              Cancel
            </Button>
            <Button variant="primary" loading={isLoading} onClick={submit}>
              Record payment
            </Button>
          </>
        )
      }
    >
      {result ? (
        <div className="flex flex-col gap-2">
          <p className="text-sm">Spread over {result.length === 1 ? 'one order' : `${result.length} orders`}, oldest first:</p>
          <ul className="divide-y divide-line rounded-md border border-line text-sm">
            {result.map((payment) => (
              <li key={payment.id} className="flex justify-between px-3 py-2">
                <Link className="text-accent hover:underline" to={`/sales/orders/${payment.orderId}`} onClick={onClose}>
                  Order {payment.orderId.slice(0, 8)}
                </Link>
                <span>
                  {formatPeso(payment.amount)} · {formatDate(payment.paidOn)}
                </span>
              </li>
            ))}
          </ul>
        </div>
      ) : (
        <form className="flex flex-col gap-3" onSubmit={submit}>
          <p className="text-sm text-muted">
            The tab owes <span className="font-semibold text-ink">{formatPeso(customer.balance)}</span>. The amount is
            applied to the oldest unpaid orders first.
          </p>
          <PaymentFields register={register} errors={formState.errors} autoFocus />
          <button type="submit" hidden />
        </form>
      )}
    </Modal>
  )
}
