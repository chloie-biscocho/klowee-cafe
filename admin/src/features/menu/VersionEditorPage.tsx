import { useState } from 'react'
import { Navigate, useBlocker, useParams } from 'react-router'
import {
  useGetVersionQuery,
  useListItemsQuery,
  useReplaceVersionItemsMutation,
  useSetVersionPublishedMutation,
} from '../../api/menuApi'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { Spinner } from '../../components/ui/Spinner'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import { AddItemPicker } from './AddItemPicker'
import { CopyVersionModal } from './CopyVersionModal'
import {
  moveWithinCategory,
  toEditorItems,
  toRequestItems,
  type EditorItem,
} from './editorItems'
import { VersionEditorHeader } from './VersionEditorHeader'
import { VersionItemsTable } from './VersionItemsTable'

export function VersionEditorPage() {
  const { id } = useParams<'id'>()
  if (!id) return <Navigate to="/menu/versions" replace />

  // Remounting per version is what discards a half-finished edit of another one.
  return <VersionEditor key={id} versionId={id} />
}

function VersionEditor({ versionId }: { versionId: string }) {
  const run = useToastedAction()

  const { data: version, isLoading } = useGetVersionQuery(versionId)
  const { data: catalogue, isFetching: isCatalogueLoading } = useListItemsQuery({
    includeArchived: false,
  })

  const [replaceItems, { isLoading: isSaving }] = useReplaceVersionItemsMutation()
  const [setPublished, { isLoading: isPublishing }] = useSetVersionPublishedMutation()

  /**
   * `null` means "no local edits" — the table renders straight from the cache.
   * The first change takes a copy, and a successful save drops back to `null`,
   * so the refreshed server data becomes the truth again. No `useEffect`, and
   * no way for the two to disagree.
   */
  const [draft, setDraft] = useState<EditorItem[] | null>(null)
  const [isPickerOpen, setPickerOpen] = useState(false)
  const [isCopyOpen, setCopyOpen] = useState(false)

  const items = draft ?? toEditorItems(version?.items ?? [])
  const isDirty = draft !== null
  const readOnly = version?.isPublished ?? true

  // Stops a half-finished price list from vanishing on a stray click in the nav.
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      isDirty && currentLocation.pathname !== nextLocation.pathname,
  )

  if (isLoading) {
    return <Spinner className="size-6 text-muted" />
  }

  if (!version) {
    return <EmptyState message="That menu version no longer exists." />
  }

  const onSave = async () => {
    const saved = await run(
      replaceItems({ id: versionId, items: toRequestItems(items) }),
      'Menu saved.',
    )
    if (saved.ok) setDraft(null)
  }

  const alreadyOnVersion = new Set(items.map((item) => item.menuItemId))
  const candidates = (catalogue ?? []).filter((item) => !alreadyOnVersion.has(item.id))

  return (
    <>
      <VersionEditorHeader
        version={version}
        isDirty={isDirty}
        isSaving={isSaving}
        isPublishing={isPublishing}
        onSave={onSave}
        onPublish={() =>
          run(setPublished({ id: versionId, isPublished: true }), 'Version published.')
        }
        onCopy={() => setCopyOpen(true)}
      />

      <Card>
        <CardHeader>
          <p className="text-sm text-muted">
            {readOnly
              ? "Published versions can't be edited. Copy it to make changes."
              : 'Prices, availability and order for this version only.'}
          </p>
          {!readOnly && (
            <Button size="sm" onClick={() => setPickerOpen(true)}>
              Add item
            </Button>
          )}
        </CardHeader>

        {items.length === 0 ? (
          <EmptyState
            message="No items on this version yet. Add the drinks it should offer."
            action={
              !readOnly && (
                <Button variant="primary" size="sm" onClick={() => setPickerOpen(true)}>
                  Add item
                </Button>
              )
            }
          />
        ) : (
          <VersionItemsTable
            items={items}
            readOnly={readOnly}
            onChange={(index, patch) =>
              setDraft(items.map((item, at) => (at === index ? { ...item, ...patch } : item)))
            }
            onMove={(index, direction) => setDraft(moveWithinCategory(items, index, direction))}
            onRemove={(index) => setDraft(items.filter((_, at) => at !== index))}
          />
        )}
      </Card>

      {isPickerOpen && (
        <AddItemPicker
          open
          loading={isCatalogueLoading}
          candidates={candidates}
          onClose={() => setPickerOpen(false)}
          onAdd={(added) => {
            setDraft([
              ...items,
              ...added.map((item) => ({
                menuItemId: item.id,
                name: item.name,
                categoryName: item.categoryName,
                price: '0',
                isAvailable: true,
                isFeatured: false,
              })),
            ])
            setPickerOpen(false)
          }}
        />
      )}

      {isCopyOpen && (
        <CopyVersionModal version={version} onClose={() => setCopyOpen(false)} />
      )}

      <ConfirmDialog
        open={blocker.state === 'blocked'}
        title="Leave without saving?"
        message="This version has unsaved price or availability changes. They will be lost."
        confirmLabel="Leave"
        onCancel={() => blocker.reset?.()}
        onConfirm={() => blocker.proceed?.()}
      />
    </>
  )
}
