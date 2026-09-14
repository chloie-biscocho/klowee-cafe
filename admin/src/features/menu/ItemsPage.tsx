import { useState } from 'react'
import {
  useCreateItemMutation,
  useDeleteItemMutation,
  useListCategoriesQuery,
  useListItemsQuery,
  useSetItemArchivedMutation,
  useUpdateItemMutation,
} from '../../api/menuApi'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { SkeletonRows, Table, Td, Th } from '../../components/ui/Table'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import type { MenuItemDto } from '../../types/api'
import { ItemForm } from './ItemForm'
import { PhotoThumbnail } from './PhotoThumbnail'
import type { ItemValues } from './schemas'

/** The form keeps empty strings; the API wants nulls for "not set". */
function toRequest(values: ItemValues) {
  return {
    name: values.name,
    description: values.description === '' ? null : values.description,
    categoryId: values.categoryId,
    photoUrl: values.photoUrl === '' ? null : values.photoUrl,
  }
}

export function ItemsPage() {
  const [includeArchived, setIncludeArchived] = useState(false)
  const { data: items, isLoading, isFetching } = useListItemsQuery({ includeArchived })
  const { data: categories } = useListCategoriesQuery()

  const [createItem, { isLoading: isCreating }] = useCreateItemMutation()
  const [updateItem, { isLoading: isUpdating }] = useUpdateItemMutation()
  const [setArchived] = useSetItemArchivedMutation()
  const [deleteItem, { isLoading: isDeleting }] = useDeleteItemMutation()
  const run = useToastedAction()

  const [editing, setEditing] = useState<MenuItemDto | null>(null)
  const [isFormOpen, setFormOpen] = useState(false)
  const [pendingDelete, setPendingDelete] = useState<MenuItemDto | null>(null)

  const openForm = (item: MenuItemDto | null) => {
    setEditing(item)
    setFormOpen(true)
  }

  return (
    <>
      <Card>
        <CardHeader>
          <label className="flex items-center gap-2 text-sm text-muted">
            <input
              type="checkbox"
              className="size-4 accent-accent"
              checked={includeArchived}
              onChange={(event) => setIncludeArchived(event.target.checked)}
            />
            Show archived
          </label>
          <Button
            variant="primary"
            size="sm"
            onClick={() => openForm(null)}
            disabled={(categories?.length ?? 0) === 0}
          >
            New item
          </Button>
        </CardHeader>

        <Table>
          <thead>
            <tr>
              <Th className="w-14">Photo</Th>
              <Th className="w-[40%]">Name</Th>
              <Th>Category</Th>
              <Th>Status</Th>
              <Th className="text-right">Actions</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <SkeletonRows columns={5} />}
            {items?.map((item) => (
              <tr key={item.id} className={isFetching ? 'opacity-60' : undefined}>
                <Td>
                  <PhotoThumbnail url={item.photoUrl} name={item.name} />
                </Td>
                <Td>
                  <span className="font-medium">{item.name}</span>
                  {item.description && (
                    <p className="mt-0.5 line-clamp-1 text-xs text-muted">{item.description}</p>
                  )}
                </Td>
                <Td className="text-muted">{item.categoryName}</Td>
                <Td>
                  <Badge tone={item.isArchived ? 'neutral' : 'success'}>
                    {item.isArchived ? 'Archived' : 'Active'}
                  </Badge>
                </Td>
                <Td className="text-right">
                  <div className="flex justify-end gap-1.5">
                    <Button size="sm" onClick={() => openForm(item)}>
                      Edit
                    </Button>
                    <Button
                      size="sm"
                      onClick={() =>
                        run(
                          setArchived({ id: item.id, isArchived: !item.isArchived }),
                          item.isArchived ? 'Item unarchived.' : 'Item archived.',
                        )
                      }
                    >
                      {item.isArchived ? 'Unarchive' : 'Archive'}
                    </Button>
                    <Button size="sm" variant="danger" onClick={() => setPendingDelete(item)}>
                      Delete
                    </Button>
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>

        {!isLoading && (items?.length ?? 0) === 0 && (
          <EmptyState
            message={
              (categories?.length ?? 0) === 0
                ? 'Add a category first — every item belongs to one.'
                : 'No menu items yet. Create your first one.'
            }
            action={
              (categories?.length ?? 0) > 0 && (
                <Button variant="primary" size="sm" onClick={() => openForm(null)}>
                  New item
                </Button>
              )
            }
          />
        )}
      </Card>

      {isFormOpen && (
        <ItemForm
          key={editing?.id ?? 'new'}
          open
          item={editing}
          categories={categories ?? []}
          saving={isCreating || isUpdating}
          onClose={() => setFormOpen(false)}
          onSubmit={async (values) => {
            const body = toRequest(values)
            const saved = editing
              ? await run(updateItem({ id: editing.id, body }), 'Item updated.')
              : await run(createItem(body), 'Item created.')
            if (saved.ok) setFormOpen(false)
          }}
        />
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete menu item"
        message={`Delete "${pendingDelete?.name}"? Items priced on a menu version cannot be deleted — archive them instead.`}
        loading={isDeleting}
        onCancel={() => setPendingDelete(null)}
        onConfirm={async () => {
          if (!pendingDelete) return
          const deleted = await run(deleteItem(pendingDelete.id), 'Item deleted.')
          if (deleted.ok) setPendingDelete(null)
        }}
      />
    </>
  )
}
