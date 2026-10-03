import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { Textarea } from '../../components/ui/Textarea'
import type { PackageDto } from '../../types/api'
import { InclusionsEditor } from './InclusionsEditor'
import { packageSchema, type PackageValues } from './schemas'

export function PackageForm({
  pkg,
  nextSortOrder,
  saving,
  onClose,
  onSubmit,
}: {
  /** `null` means "new package". */
  pkg: PackageDto | null
  /** Where a new package lands by default: after the last one. */
  nextSortOrder: number
  saving: boolean
  onClose: () => void
  onSubmit: (values: PackageValues) => void
}) {
  const { register, handleSubmit, control, formState } = useForm<PackageValues>({
    resolver: zodResolver(packageSchema),
    defaultValues: {
      name: pkg?.name ?? '',
      price: pkg?.price ?? 0,
      description: pkg?.description ?? '',
      guestCountNote: pkg?.guestCountNote ?? '',
      sortOrder: pkg?.sortOrder ?? nextSortOrder,
      isActive: pkg?.isActive ?? true,
      inclusions: (pkg?.inclusions ?? []).map((inclusion) => ({ text: inclusion.text })),
    },
  })
  const { errors } = formState

  return (
    <Modal
      open
      title={pkg ? 'Edit package' : 'New package'}
      onClose={onClose}
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
        <Input label="Name" autoFocus error={errors.name?.message} {...register('name')} />
        <div className="grid grid-cols-2 gap-3">
          <Input
            label="Price"
            type="number"
            min="0"
            step="1"
            hint="In pesos."
            error={errors.price?.message}
            {...register('price')}
          />
          <Input
            label="Sort order"
            type="number"
            min="0"
            hint="Lower shows first."
            error={errors.sortOrder?.message}
            {...register('sortOrder')}
          />
        </div>
        <Input
          label="Guest count note"
          placeholder="Good for 60 pax"
          error={errors.guestCountNote?.message}
          {...register('guestCountNote')}
        />
        <Textarea
          label="Description"
          error={errors.description?.message}
          {...register('description')}
        />
        <InclusionsEditor control={control} register={register} errors={errors.inclusions} />
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" className="size-4 accent-accent" {...register('isActive')} />
          Active (shown on the public site)
        </label>
      </div>
    </Modal>
  )
}
