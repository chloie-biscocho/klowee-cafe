import { useState } from 'react'
import {
  useCreateAddOnMutation,
  useDeleteAddOnMutation,
  useListAddOnsQuery,
  useUpdateAddOnMutation,
} from '../../api/menuApi'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { SkeletonRows, Table, Td, Th } from '../../components/ui/Table'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import { formatPeso } from '../../lib/format'
import type { AddOnDto } from '../../types/api'
import { AddOnForm } from './AddOnForm'

export function AddOnsPage() {
  const { data: addOns, isLoading } = useListAddOnsQuery()
  const [createAddOn, { isLoading: isCreating }] = useCreateAddOnMutation()
  const [updateAddOn, { isLoading: isUpdating }] = useUpdateAddOnMutation()
  const [deleteAddOn, { isLoading: isDeleting }] = useDeleteAddOnMutation()
  const run = useToastedAction()

  const [editing, setEditing] = useState<AddOnDto | null>(null)
  const [isFormOpen, setFormOpen] = useState(false)
  const [pendingDelete, setPendingDelete] = useState<AddOnDto | null>(null)

  const openForm = (addOn: AddOnDto | null) => {
    setEditing(addOn)
    setFormOpen(true)
  }

  return (
    <>
      <Card>
        <CardHeader>
          <p className="text-sm text-muted">Extras that can be added to any drink.</p>
          <Button variant="primary" size="sm" onClick={() => openForm(null)}>
            New add-on
          </Button>
        </CardHeader>

        <Table>
          <thead>
            <tr>
              <Th className="w-[50%]">Name</Th>
              <Th>Price</Th>
              <Th>Status</Th>
              <Th className="text-right">Actions</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <SkeletonRows columns={4} />}
            {addOns?.map((addOn) => (
              <tr key={addOn.id}>
                <Td className="font-medium">{addOn.name}</Td>
                <Td>{formatPeso(addOn.price)}</Td>
                <Td>
                  <Badge tone={addOn.isActive ? 'success' : 'neutral'}>
                    {addOn.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </Td>
                <Td className="text-right">
                  <div className="flex justify-end gap-1.5">
                    <Button
                      size="sm"
                      onClick={() =>
                        run(
                          updateAddOn({
                            id: addOn.id,
                            body: { ...addOn, isActive: !addOn.isActive },
                          }),
                          addOn.isActive ? 'Add-on deactivated.' : 'Add-on activated.',
                        )
                      }
                    >
                      {addOn.isActive ? 'Deactivate' : 'Activate'}
                    </Button>
                    <Button size="sm" onClick={() => openForm(addOn)}>
                      Edit
                    </Button>
                    <Button size="sm" variant="danger" onClick={() => setPendingDelete(addOn)}>
                      Delete
                    </Button>
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>

        {!isLoading && (addOns?.length ?? 0) === 0 && (
          <EmptyState
            message="No add-ons yet. Create your first one."
            action={
              <Button variant="primary" size="sm" onClick={() => openForm(null)}>
                New add-on
              </Button>
            }
          />
        )}
      </Card>

      {isFormOpen && (
        <AddOnForm
          // Remounting per add-on is what resets the form to that row's values.
          key={editing?.id ?? 'new'}
          open
          addOn={editing}
          saving={isCreating || isUpdating}
          onClose={() => setFormOpen(false)}
          onSubmit={async (values) => {
            const saved = editing
              ? await run(updateAddOn({ id: editing.id, body: values }), 'Add-on updated.')
              : await run(createAddOn(values), 'Add-on created.')
            if (saved.ok) setFormOpen(false)
          }}
        />
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete add-on"
        message={`Delete "${pendingDelete?.name}"? This cannot be undone from the admin app.`}
        loading={isDeleting}
        onCancel={() => setPendingDelete(null)}
        onConfirm={async () => {
          if (!pendingDelete) return
          const deleted = await run(deleteAddOn(pendingDelete.id), 'Add-on deleted.')
          if (deleted.ok) setPendingDelete(null)
        }}
      />
    </>
  )
}
