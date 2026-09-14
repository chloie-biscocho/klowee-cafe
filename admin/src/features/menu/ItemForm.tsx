import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { Select } from '../../components/ui/Select'
import { Textarea } from '../../components/ui/Textarea'
import type { MenuCategoryDto, MenuItemDto } from '../../types/api'
import { itemSchema, type ItemValues } from './schemas'

export function ItemForm({
  open,
  item,
  categories,
  saving,
  onClose,
  onSubmit,
}: {
  open: boolean
  /** `null` means "new item". */
  item: MenuItemDto | null
  categories: MenuCategoryDto[]
  saving: boolean
  onClose: () => void
  onSubmit: (values: ItemValues) => void
}) {
  const { register, handleSubmit, formState } = useForm<ItemValues>({
    resolver: zodResolver(itemSchema),
    defaultValues: {
      name: item?.name ?? '',
      description: item?.description ?? '',
      categoryId: item?.categoryId ?? (categories[0]?.id ?? ''),
      photoUrl: item?.photoUrl ?? '',
    },
  })

  return (
    <Modal
      open={open}
      title={item ? 'Edit menu item' : 'New menu item'}
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
        <Input label="Name" autoFocus error={formState.errors.name?.message} {...register('name')} />
        <Textarea
          label="Description"
          hint="Shown on the public site. Optional."
          error={formState.errors.description?.message}
          {...register('description')}
        />
        <Select
          label="Category"
          error={formState.errors.categoryId?.message}
          {...register('categoryId')}
        >
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.name}
            </option>
          ))}
        </Select>
        <Input
          label="Photo URL"
          placeholder="https://…"
          hint="A link for now. Uploading comes later."
          error={formState.errors.photoUrl?.message}
          {...register('photoUrl')}
        />
      </div>
    </Modal>
  )
}
