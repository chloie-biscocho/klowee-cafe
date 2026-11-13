import { zodResolver } from '@hookform/resolvers/zod'
import { useForm, type UseFormRegisterReturn } from 'react-hook-form'
import type { z } from 'zod'
import { Button } from '../../components/ui/Button'
import { Td } from '../../components/ui/Table'
import type { LoyaltyClubDto, MenuCategoryDto } from '../../types/api'
import { categorySchema, type CategoryValues } from './schemas'

type CategoryInput = z.input<typeof categorySchema>

const cellInput =
  'h-8 w-full rounded-md border border-line bg-surface px-2 text-sm focus:border-accent'

/** The editing state of an existing row: the same three columns, as inputs. */
export function CategoryRow({
  category,
  clubs,
  saving,
  onSave,
  onCancel,
}: {
  category: MenuCategoryDto
  clubs: LoyaltyClubDto[]
  saving: boolean
  onSave: (values: CategoryValues) => void
  onCancel: () => void
}) {
  const { register, handleSubmit, formState } = useForm<CategoryInput, unknown, CategoryValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: {
      name: category.name,
      sortOrder: category.sortOrder,
      description: category.description ?? '',
      loyaltyClubId: category.loyaltyClubId ?? '',
    },
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
        <DescriptionInput label="Category description" register={register('description')} error={formState.errors.description?.message} />
      </Td>
      <Td>
        <ClubSelect label="Loyalty card" clubs={clubs} register={register('loyaltyClubId')} />
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
  clubs,
  saving,
  nextSortOrder,
  onCreate,
}: {
  clubs: LoyaltyClubDto[]
  saving: boolean
  nextSortOrder: number
  onCreate: (values: CategoryValues) => Promise<{ ok: boolean }>
}) {
  const { register, handleSubmit, reset, formState } = useForm<CategoryInput, unknown, CategoryValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: '', sortOrder: nextSortOrder, description: '', loyaltyClubId: '' },
  })

  const submit = handleSubmit(async (values) => {
    const created = await onCreate(values)
    if (created.ok) reset({ name: '', sortOrder: values.sortOrder + 10, description: '', loyaltyClubId: '' })
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
        <DescriptionInput label="New category description" register={register('description')} error={formState.errors.description?.message} />
      </Td>
      <Td>
        <ClubSelect label="New category loyalty card" clubs={clubs} register={register('loyaltyClubId')} />
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

/** Which loyalty card the category's drinks stamp (decision 020); items can override it. */
function ClubSelect({ label, clubs, register }: { label: string; clubs: LoyaltyClubDto[]; register: UseFormRegisterReturn }) {
  return (
    <select aria-label={label} className={cellInput} {...register}>
      <option value="">No card</option>
      {clubs.map((club) => (
        <option key={club.id} value={club.id}>
          {club.name}
          {club.isActive ? '' : ' (not active)'}
        </option>
      ))}
    </select>
  )
}

/** The optional line shown under a category's name on the public menu. */
function DescriptionInput({ label, register, error }: { label: string; register: UseFormRegisterReturn; error?: string }) {
  return (
    <>
      <input aria-label={label} placeholder="Description (optional)" className={`${cellInput} mt-1.5`} {...register} />
      {error && <p className="mt-1 text-xs text-danger">{error}</p>}
    </>
  )
}
