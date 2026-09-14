import { useState } from 'react'
import {
  useCreateVersionMutation,
  useDeleteVersionMutation,
  useListVersionsQuery,
  useSetVersionPublishedMutation,
  useUpdateVersionMutation,
} from '../../api/menuApi'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { SkeletonRows, Table, Th } from '../../components/ui/Table'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import {
  MENU_CONTEXTS,
  MENU_CONTEXT_LABELS,
  type MenuContext,
  type MenuVersionSummaryDto,
} from '../../types/api'
import { VersionFormModal } from './VersionFormModal'
import { VersionRow } from './VersionRow'

export function VersionsPage() {
  const [context, setContext] = useState<MenuContext | ''>('')
  const { data: versions, isLoading } = useListVersionsQuery(
    context === '' ? {} : { context },
  )
  const { data: allVersions } = useListVersionsQuery({})

  const [createVersion, { isLoading: isCreating }] = useCreateVersionMutation()
  const [updateVersion, { isLoading: isUpdating }] = useUpdateVersionMutation()
  const [setPublished] = useSetVersionPublishedMutation()
  const [deleteVersion, { isLoading: isDeleting }] = useDeleteVersionMutation()
  const run = useToastedAction()

  const [isFormOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<MenuVersionSummaryDto | null>(null)
  const [pendingDelete, setPendingDelete] = useState<MenuVersionSummaryDto | null>(null)

  const openForm = (version: MenuVersionSummaryDto | null) => {
    setEditing(version)
    setFormOpen(true)
  }

  return (
    <>
      <Card>
        <CardHeader>
          <label className="flex items-center gap-2 text-sm text-muted">
            Context
            <select
              className="h-8 rounded-md border border-line bg-surface px-2 text-sm text-ink"
              value={context}
              onChange={(event) => setContext(event.target.value as MenuContext | '')}
            >
              <option value="">All</option>
              {MENU_CONTEXTS.map((value) => (
                <option key={value} value={value}>
                  {MENU_CONTEXT_LABELS[value]}
                </option>
              ))}
            </select>
          </label>
          <Button variant="primary" size="sm" onClick={() => openForm(null)}>
            New version
          </Button>
        </CardHeader>

        <Table>
          <thead>
            <tr>
              <Th className="w-[35%]">Name</Th>
              <Th>Context</Th>
              <Th>Effective from</Th>
              <Th>Items</Th>
              <Th>Status</Th>
              <Th className="text-right">Actions</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <SkeletonRows columns={6} />}
            {versions?.map((version) => (
              <VersionRow
                key={version.id}
                version={version}
                onEdit={() => openForm(version)}
                onDelete={() => setPendingDelete(version)}
                onTogglePublish={() =>
                  run(
                    setPublished({ id: version.id, isPublished: !version.isPublished }),
                    version.isPublished ? 'Version unpublished.' : 'Version published.',
                  )
                }
              />
            ))}
          </tbody>
        </Table>

        {!isLoading && (versions?.length ?? 0) === 0 && (
          <EmptyState
            message="No versions yet. Create your first one."
            action={
              <Button variant="primary" size="sm" onClick={() => openForm(null)}>
                New version
              </Button>
            }
          />
        )}
      </Card>

      {isFormOpen && (
        <VersionFormModal
          key={editing?.id ?? 'new'}
          open
          editing={editing}
          versions={allVersions ?? []}
          initial={context === '' ? undefined : { context }}
          saving={isCreating || isUpdating}
          onClose={() => setFormOpen(false)}
          onSubmit={async (values) => {
            const saved = editing
              ? await run(
                  updateVersion({
                    id: editing.id,
                    body: {
                      name: values.name,
                      effectiveFrom: values.effectiveFrom,
                      notes: values.notes === '' ? null : values.notes,
                    },
                  }),
                  'Version updated.',
                )
              : await run(
                  createVersion({
                    name: values.name,
                    context: values.context,
                    effectiveFrom: values.effectiveFrom,
                    notes: values.notes === '' ? null : values.notes,
                    copyFromVersionId:
                      values.copyFromVersionId === '' ? null : values.copyFromVersionId,
                  }),
                  'Draft version created.',
                )
            if (saved.ok) setFormOpen(false)
          }}
        />
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete draft version"
        message={`Delete "${pendingDelete?.name}" and its items? Published versions cannot be deleted.`}
        loading={isDeleting}
        onCancel={() => setPendingDelete(null)}
        onConfirm={async () => {
          if (!pendingDelete) return
          const deleted = await run(deleteVersion(pendingDelete.id), 'Draft version deleted.')
          if (deleted.ok) setPendingDelete(null)
        }}
      />
    </>
  )
}
