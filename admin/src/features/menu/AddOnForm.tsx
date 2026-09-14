import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import type { AddOnDto } from '../../types/api'
import { addOnSchema, type AddOnValues } from './schemas'

export function AddOnForm({
  open,
  addOn,
  saving,
  onClose,
  onSubmit,
}: {
  open: boolean
  /** `null` means "new add-on". */
  addOn: AddOnDto | null
  saving: boolean
  onClose: () => void
  onSubmit: (values: AddOnValues) => void
}) {
  const { register, handleSubmit, formState } = useForm<AddOnValues>({
    resolver: zodResolver(addOnSchema),
    defaultValues: {
      name: addOn?.name ?? '',
      price: addOn?.price ?? 0,
      isActive: addOn?.isActive ?? true,
    },
  })

  return (
    <Modal
      open={open}
      title={addOn ? 'Edit add-on' : 'New add-on'}
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
      <div className="flex flex-col gap-4">
        <Input label="Name" autoFocus error={formState.errors.name?.message} {...register('name')} />
        <Input
          label="Price"
          type="number"
          step="0.5"
          min="0"
          hint="In pesos. 0 is allowed for a free extra."
          error={formState.errors.price?.message}
          {...register('price')}
        />
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" className="size-4 accent-accent" {...register('isActive')} />
          Active (offered to customers)
        </label>
      </div>
    </Modal>
  )
}
