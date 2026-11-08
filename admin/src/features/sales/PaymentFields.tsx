import type { FieldErrors, UseFormRegister } from 'react-hook-form'
import { Input } from '../../components/ui/Input'
import { Select } from '../../components/ui/Select'
import { PAYMENT_CHANNELS, PAYMENT_CHANNEL_LABELS } from '../../types/api'
import type { PaymentValues } from './schemas'

/** Amount, channel, date and reference: the same four fields wherever money is taken. */
export function PaymentFields({
  register,
  errors,
  autoFocus,
}: {
  register: UseFormRegister<PaymentValues>
  errors: FieldErrors<PaymentValues>
  autoFocus?: boolean
}) {
  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
      <Input
        label="Amount"
        type="number"
        min="0"
        step="0.01"
        autoFocus={autoFocus}
        error={errors.amount?.message}
        {...register('amount')}
      />
      <Select label="Channel" error={errors.channel?.message} {...register('channel')}>
        {PAYMENT_CHANNELS.map((channel) => (
          <option key={channel} value={channel}>
            {PAYMENT_CHANNEL_LABELS[channel]}
          </option>
        ))}
      </Select>
      <Input label="Paid on" type="date" error={errors.paidOn?.message} {...register('paidOn')} />
      <Input label="Reference" placeholder="Optional" error={errors.reference?.message} {...register('reference')} />
    </div>
  )
}
