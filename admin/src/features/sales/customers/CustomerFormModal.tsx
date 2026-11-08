import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { Modal } from '../../../components/ui/Modal'
import { Textarea } from '../../../components/ui/Textarea'
import type { CustomerSummaryDto } from '../../../types/api'
import { customerSchema, type CustomerValues } from '../schemas'

/** New or edit. `initialName` pre-fills a new customer from what was typed in a picker. */
export function CustomerFormModal({
  customer,
  initialName = '',
  compact = false,
  saving,
  onClose,
  onSubmit,
}: {
  customer: CustomerSummaryDto | null
  initialName?: string
  /** The order form's quick add: name and nickname only. */
  compact?: boolean
  saving: boolean
  onClose: () => void
  onSubmit: (values: CustomerValues) => void
}) {
  const { register, handleSubmit, formState } = useForm<CustomerValues>({
    resolver: zodResolver(customerSchema),
    defaultValues: {
      name: customer?.name ?? initialName,
      nickname: customer?.nickname ?? '',
      notes: customer?.notes ?? '',
    },
  })
  const { errors } = formState

  return (
    <Modal
      open
      title={customer ? 'Edit customer' : 'New customer'}
      onClose={onClose}
      width="max-w-md"
      footer={
        <>
          <Button onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button variant="primary" loading={saving} onClick={handleSubmit(onSubmit)}>
            Save
          </Button>
        </>
      }
    >
      <form className="flex flex-col gap-4" onSubmit={handleSubmit(onSubmit)}>
        <Input label="Name" autoFocus error={errors.name?.message} {...register('name')} />
        <Input label="Nickname" placeholder="Optional" error={errors.nickname?.message} {...register('nickname')} />
        {!compact && <Textarea label="Notes" error={errors.notes?.message} {...register('notes')} />}
        {/* Enter in a field submits. */}
        <button type="submit" hidden />
      </form>
    </Modal>
  )
}
