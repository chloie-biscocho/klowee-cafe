import { useState } from 'react'
import {
  useCreateCategoryMutation,
  useDeleteCategoryMutation,
  useListCategoriesQuery,
  useUpdateCategoryMutation,
} from '../../api/menuApi'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { SkeletonRows, Table, Td, Th } from '../../components/ui/Table'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import type { MenuCategoryDto } from '../../types/api'
import { CategoryRow, NewCategoryRow } from './CategoryRow'

export function CategoriesPage() {
  const { data: categories, isLoading } = useListCategoriesQuery()
  const [createCategory, { isLoading: isCreating }] = useCreateCategoryMutation()
  const [updateCategory, { isLoading: isUpdating }] = useUpdateCategoryMutation()
  const [deleteCategory, { isLoading: isDeleting }] = useDeleteCategoryMutation()
  const run = useToastedAction()

  const [editingId, setEditingId] = useState<string | null>(null)
  const [pendingDelete, setPendingDelete] = useState<MenuCategoryDto | null>(null)

  const isEmpty = !isLoading && (categories?.length ?? 0) === 0

  return (
    <>
      <p className="mb-4 max-w-prose text-sm text-muted">
        Categories group menu items and set the order they appear in on a menu.
      </p>

      <Card>
        <Table>
          <thead>
            <tr>
              <Th className="w-[55%]">Name</Th>
              <Th className="w-[20%]">Sort order</Th>
              <Th className="text-right">Actions</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <SkeletonRows columns={3} />}

            {categories?.map((category) =>
              editingId === category.id ? (
                <CategoryRow
                  key={category.id}
                  category={category}
                  saving={isUpdating}
                  onCancel={() => setEditingId(null)}
                  onSave={async (values) => {
                    const saved = await run(
                      updateCategory({ id: category.id, body: values }),
                      'Category updated.',
                    )
                    if (saved.ok) setEditingId(null)
                  }}
                />
              ) : (
                <tr key={category.id}>
                  <Td className="font-medium">{category.name}</Td>
                  <Td className="text-muted">{category.sortOrder}</Td>
                  <Td className="text-right">
                    <div className="flex justify-end gap-1.5">
                      <Button size="sm" onClick={() => setEditingId(category.id)}>
                        Edit
                      </Button>
                      <Button
                        size="sm"
                        variant="danger"
                        onClick={() => setPendingDelete(category)}
                      >
                        Delete
                      </Button>
                    </div>
                  </Td>
                </tr>
              ),
            )}

            {!isLoading && (
              <NewCategoryRow
                saving={isCreating}
                nextSortOrder={(categories?.at(-1)?.sortOrder ?? -10) + 10}
                onCreate={(values) => run(createCategory(values), 'Category added.')}
              />
            )}
          </tbody>
        </Table>

        {isEmpty && (
          <EmptyState message="No categories yet. Add your first one in the row above." />
        )}
      </Card>

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete category"
        message={`Delete "${pendingDelete?.name}"? Categories still used by a menu item cannot be deleted.`}
        loading={isDeleting}
        onCancel={() => setPendingDelete(null)}
        onConfirm={async () => {
          if (!pendingDelete) return
          const deleted = await run(deleteCategory(pendingDelete.id), 'Category deleted.')
          if (deleted.ok) setPendingDelete(null)
        }}
      />
    </>
  )
}
