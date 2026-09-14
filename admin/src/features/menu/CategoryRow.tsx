import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Button } from '../../components/ui/Button'
import { Td } from '../../components/ui/Table'
import type { MenuCategoryDto } from '../../types/api'
import { categorySchema, type CategoryValues } from './schemas'

const cellInput =
  'h-8 w-full rounded-md border border-line bg-surface px-2 text-sm focus:border-accent'

/** The editing state of an existing row: the same three columns, as inputs. */
export function CategoryRow({
  category,
  saving,
  onSave,
  onCancel,
}: {
  category: MenuCategoryDto
  saving: boolean
  onSave: (values: CategoryValues) => void
  onCancel: () => void
}) {
  const { register, handleSubmit, formState } = useForm<CategoryValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: category.name, sortOrder: category.sortOrder },
  })

  return (
    <tr>
      <Td>
        <input
          aria-label="Category name"
          className={cellInput}
          autoFocus
          {...register('name')}
        />
        {formState.errors.name && (
          <p className="mt-1 text-xs text-danger">{formState.errors.name.message}</p>
        )}
      </Td>
      <Td>
        <input
          aria-label="Sort order"
          type="number"
          className={cellInput}
          {...register('sortOrder')}
        />
      </Td>
      <Td className="text-right">
        <div className="flex justify-end gap-1.5">
          <Button size="sm" variant="primary" loading={saving} onClick={handleSubmit(onSave)}>
            Save
          </Button>
          <Button size="sm" variant="ghost" onClick={onCancel} disabled={saving}>
            Cancel
          </Button>
        </div>
      </Td>
    </tr>
  )
}

/** The always-present last row, so adding a category takes no modal. */
export function NewCategoryRow({
  saving,
  nextSortOrder,
  onCreate,
}: {
  saving: boolean
  nextSortOrder: number
  onCreate: (values: CategoryValues) => Promise<{ ok: boolean }>
}) {
  const { register, handleSubmit, reset, formState } = useForm<CategoryValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: '', sortOrder: nextSortOrder },
  })

  const submit = handleSubmit(async (values) => {
    const created = await onCreate(values)
    if (created.ok) reset({ name: '', sortOrder: values.sortOrder + 10 })
  })

  return (
    <tr className="bg-canvas/60">
      <Td>
        <input
          aria-label="New category name"
          placeholder="New category"
          className={cellInput}
          {...register('name')}
        />
        {formState.errors.name && (
          <p className="mt-1 text-xs text-danger">{formState.errors.name.message}</p>
        )}
      </Td>
      <Td>
        <input
          aria-label="New category sort order"
          type="number"
          className={cellInput}
          {...register('sortOrder')}
        />
      </Td>
      <Td className="text-right">
        <Button size="sm" variant="primary" loading={saving} onClick={submit}>
          Add
        </Button>
      </Td>
    </tr>
  )
}
