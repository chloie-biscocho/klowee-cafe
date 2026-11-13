import { zodResolver } from '@hookform/resolvers/zod'
import { Controller, useForm } from 'react-hook-form'
import { Button } from '../../components/ui/Button'
import { ImageUpload } from '../../components/ui/ImageUpload'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { Select } from '../../components/ui/Select'
import { Textarea } from '../../components/ui/Textarea'
import { loyaltyChoice } from '../../lib/loyalty'
import type { LoyaltyClubDto, MenuCategoryDto, MenuItemDto } from '../../types/api'
import { itemSchema, type ItemValues } from './schemas'

export function ItemForm({
  open,
  item,
  categories,
  clubs,
  saving,
  onClose,
  onSubmit,
}: {
  open: boolean
  /** `null` means "new item". */
  item: MenuItemDto | null
  categories: MenuCategoryDto[]
  clubs: LoyaltyClubDto[]
  saving: boolean
  onClose: () => void
  onSubmit: (values: ItemValues) => void
}) {
  const { register, handleSubmit, control, formState } = useForm<ItemValues>({
    resolver: zodResolver(itemSchema),
    defaultValues: {
      name: item?.name ?? '',
      description: item?.description ?? '',
      categoryId: item?.categoryId ?? (categories[0]?.id ?? ''),
      photoUrl: item?.photoUrl ?? null,
      loyalty: loyaltyChoice(item?.loyaltyMode ?? 'Inherit', item?.loyaltyClubId ?? null),
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
        <Select
          label="Loyalty card"
          hint="Which card this drink's paid cups stamp. Usually its category's; Dirty Matcha, say, sits in Coffee but stamps The Matcha Club."
          {...register('loyalty')}
        >
          <option value="inherit">Same as its category</option>
          <option value="none">None (earns no stamps)</option>
          {clubs.map((club) => (
            <option key={club.id} value={club.id}>
              {club.name}
              {club.isActive ? '' : ' (not active)'}
            </option>
          ))}
        </Select>
        <Controller
          control={control}
          name="photoUrl"
          render={({ field }) => (
            <ImageUpload label="Photo" folder="menu" value={field.value} onChange={field.onChange} />
          )}
        />
      </div>
    </Modal>
  )
}
