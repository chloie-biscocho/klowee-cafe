import { useState } from 'react'
import {
  useCreatePackageMutation,
  useDeletePackageMutation,
  useListPackagesQuery,
  useSetPackageActiveMutation,
  useUpdatePackageMutation,
} from '../../api/packagesApi'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { SkeletonRows, Table, Td, Th } from '../../components/ui/Table'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import { formatPeso } from '../../lib/format'
import type { PackageDto, PackageRequest } from '../../types/api'
import { PackageForm } from './PackageForm'
import type { PackageValues } from './schemas'

/** The whole package, inclusions in the order shown: position becomes `sortOrder`. */
function toRequest(values: PackageValues): PackageRequest {
  return {
    name: values.name,
    price: values.price,
    description: values.description,
    guestCountNote: values.guestCountNote,
    sortOrder: values.sortOrder,
    isActive: values.isActive,
    inclusions: values.inclusions.map((inclusion, index) => ({
      text: inclusion.text,
      sortOrder: index,
    })),
  }
}

export function PackagesPage() {
  const [includeInactive, setIncludeInactive] = useState(false)
  const { data: packages, isLoading, isFetching } = useListPackagesQuery({ includeInactive })

  const [createPackage, { isLoading: isCreating }] = useCreatePackageMutation()
  const [updatePackage, { isLoading: isUpdating }] = useUpdatePackageMutation()
  const [setActive] = useSetPackageActiveMutation()
  const [deletePackage, { isLoading: isDeleting }] = useDeletePackageMutation()
  const run = useToastedAction()

  const [editing, setEditing] = useState<PackageDto | null>(null)
  const [isFormOpen, setFormOpen] = useState(false)
  const [pendingDelete, setPendingDelete] = useState<PackageDto | null>(null)

  const openForm = (pkg: PackageDto | null) => {
    setEditing(pkg)
    setFormOpen(true)
  }

  const nextSortOrder = Math.max(-1, ...(packages ?? []).map((pkg) => pkg.sortOrder)) + 1

  return (
    <>
      <Card>
        <CardHeader>
          <label className="flex items-center gap-2 text-sm text-muted">
            <input
              type="checkbox"
              className="size-4 accent-accent"
              checked={includeInactive}
              onChange={(event) => setIncludeInactive(event.target.checked)}
            />
            Show inactive
          </label>
          <Button variant="primary" size="sm" onClick={() => openForm(null)}>
            New package
          </Button>
        </CardHeader>

        <Table>
          <thead>
            <tr>
              <Th className="w-16">Order</Th>
              <Th className="w-[30%]">Name</Th>
              <Th>Price</Th>
              <Th>Guests</Th>
              <Th>Inclusions</Th>
              <Th>Status</Th>
              <Th className="text-right">Actions</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <SkeletonRows columns={7} />}
            {packages?.map((pkg) => (
              <tr key={pkg.id} className={isFetching ? 'opacity-60' : undefined}>
                <Td className="text-muted">{pkg.sortOrder}</Td>
                <Td className="font-medium">{pkg.name}</Td>
                <Td>{formatPeso(pkg.price)}</Td>
                <Td className="text-muted">{pkg.guestCountNote || '—'}</Td>
                <Td className="text-muted">{pkg.inclusions.length}</Td>
                <Td>
                  <Badge tone={pkg.isActive ? 'success' : 'neutral'}>
                    {pkg.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </Td>
                <Td className="text-right">
                  <div className="flex justify-end gap-1.5">
                    <Button size="sm" onClick={() => openForm(pkg)}>
                      Edit
                    </Button>
                    <Button
                      size="sm"
                      onClick={() =>
                        run(
                          setActive({ id: pkg.id, isActive: !pkg.isActive }),
                          pkg.isActive ? 'Package deactivated.' : 'Package activated.',
                        )
                      }
                    >
                      {pkg.isActive ? 'Deactivate' : 'Activate'}
                    </Button>
                    <Button size="sm" variant="danger" onClick={() => setPendingDelete(pkg)}>
                      Delete
                    </Button>
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>

        {!isLoading && (packages?.length ?? 0) === 0 && (
          <EmptyState
            message={
              includeInactive
                ? 'No packages yet. Create your first one.'
                : 'No active packages. Create one, or show inactive ones.'
            }
            action={
              <Button variant="primary" size="sm" onClick={() => openForm(null)}>
                New package
              </Button>
            }
          />
        )}
      </Card>

      {isFormOpen && (
        <PackageForm
          key={editing?.id ?? 'new'}
          pkg={editing}
          nextSortOrder={nextSortOrder}
          saving={isCreating || isUpdating}
          onClose={() => setFormOpen(false)}
          onSubmit={async (values) => {
            const body = toRequest(values)
            const saved = editing
              ? await run(updatePackage({ id: editing.id, body }), 'Package updated.')
              : await run(createPackage(body), 'Package created.')
            if (saved.ok) setFormOpen(false)
          }}
        />
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete package"
        message={`Delete "${pendingDelete?.name}" and its inclusions? To hide it from the site but keep it, deactivate it instead.`}
        loading={isDeleting}
        onCancel={() => setPendingDelete(null)}
        onConfirm={async () => {
          if (!pendingDelete) return
          const deleted = await run(deletePackage(pendingDelete.id), 'Package deleted.')
          if (deleted.ok) setPendingDelete(null)
        }}
      />
    </>
  )
}
