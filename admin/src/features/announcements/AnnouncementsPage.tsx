import { useState } from 'react'
import {
  useCreateAnnouncementMutation,
  useDeleteAnnouncementMutation,
  useListAnnouncementsQuery,
  useSetAnnouncementActiveMutation,
  useUpdateAnnouncementMutation,
} from '../../api/announcementsApi'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { SkeletonRows, Table, Td, Th } from '../../components/ui/Table'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import { formatManilaDateTime } from '../../lib/dates'
import type { AnnouncementDto } from '../../types/api'
import { AnnouncementForm } from './AnnouncementForm'
import { isLiveNow, toAnnouncementRequest } from './schemas'

export function AnnouncementsPage() {
  const { data: announcements, isLoading, isFetching } = useListAnnouncementsQuery()

  const [createAnnouncement, { isLoading: isCreating }] = useCreateAnnouncementMutation()
  const [updateAnnouncement, { isLoading: isUpdating }] = useUpdateAnnouncementMutation()
  const [setActive] = useSetAnnouncementActiveMutation()
  const [deleteAnnouncement, { isLoading: isDeleting }] = useDeleteAnnouncementMutation()
  const run = useToastedAction()

  const [editing, setEditing] = useState<AnnouncementDto | null>(null)
  const [isFormOpen, setFormOpen] = useState(false)
  const [pendingDelete, setPendingDelete] = useState<AnnouncementDto | null>(null)

  const openForm = (announcement: AnnouncementDto | null) => {
    setEditing(announcement)
    setFormOpen(true)
  }

  return (
    <>
      <Card>
        <CardHeader>
          <p className="text-sm text-muted">
            The site's ticker shows the newest live one. Times are Manila time.
          </p>
          <Button variant="primary" size="sm" onClick={() => openForm(null)}>
            New announcement
          </Button>
        </CardHeader>

        <Table>
          <thead>
            <tr>
              <Th className="w-[34%]">Text</Th>
              <Th>Link</Th>
              <Th>Active from</Th>
              <Th>Active until</Th>
              <Th>Status</Th>
              <Th className="text-right">Actions</Th>
            </tr>
          </thead>
          <tbody>
            {isLoading && <SkeletonRows columns={6} />}
            {announcements?.map((announcement) => (
              <tr key={announcement.id} className={isFetching ? 'opacity-60' : undefined}>
                <Td>
                  <p className="line-clamp-2 max-w-sm" title={announcement.text}>
                    {announcement.text}
                  </p>
                </Td>
                <Td className="max-w-40 truncate text-muted">
                  {announcement.linkUrl ? (
                    <a
                      href={announcement.linkUrl}
                      target="_blank"
                      rel="noreferrer"
                      className="hover:text-accent hover:underline"
                    >
                      {announcement.linkUrl.replace(/^https?:\/\//, '')}
                    </a>
                  ) : (
                    '—'
                  )}
                </Td>
                <Td className="whitespace-nowrap text-muted">
                  {formatManilaDateTime(announcement.activeFrom)}
                </Td>
                <Td className="whitespace-nowrap text-muted">
                  {announcement.activeUntil
                    ? formatManilaDateTime(announcement.activeUntil)
                    : 'open-ended'}
                </Td>
                <Td>
                  <div className="flex flex-wrap gap-1">
                    <Badge tone={announcement.isActive ? 'success' : 'neutral'}>
                      {announcement.isActive ? 'Active' : 'Inactive'}
                    </Badge>
                    {isLiveNow(announcement) && <Badge tone="accent">Live now</Badge>}
                  </div>
                </Td>
                <Td className="text-right">
                  <div className="flex justify-end gap-1.5">
                    <Button size="sm" onClick={() => openForm(announcement)}>
                      Edit
                    </Button>
                    <Button
                      size="sm"
                      onClick={() =>
                        run(
                          setActive({ id: announcement.id, isActive: !announcement.isActive }),
                          announcement.isActive
                            ? 'Announcement deactivated.'
                            : 'Announcement activated.',
                        )
                      }
                    >
                      {announcement.isActive ? 'Deactivate' : 'Activate'}
                    </Button>
                    <Button size="sm" variant="danger" onClick={() => setPendingDelete(announcement)}>
                      Delete
                    </Button>
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </Table>

        {!isLoading && (announcements?.length ?? 0) === 0 && (
          <EmptyState
            message="No announcements yet. The ticker falls back to the text in Settings."
            action={
              <Button variant="primary" size="sm" onClick={() => openForm(null)}>
                New announcement
              </Button>
            }
          />
        )}
      </Card>

      {isFormOpen && (
        <AnnouncementForm
          key={editing?.id ?? 'new'}
          announcement={editing}
          saving={isCreating || isUpdating}
          onClose={() => setFormOpen(false)}
          onSubmit={async (values) => {
            const body = toAnnouncementRequest(values)
            const saved = editing
              ? await run(updateAnnouncement({ id: editing.id, body }), 'Announcement updated.')
              : await run(createAnnouncement(body), 'Announcement created.')
            if (saved.ok) setFormOpen(false)
          }}
        />
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete announcement"
        message="Delete this announcement? To take it off the ticker but keep it, deactivate it instead."
        loading={isDeleting}
        onCancel={() => setPendingDelete(null)}
        onConfirm={async () => {
          if (!pendingDelete) return
          const deleted = await run(deleteAnnouncement(pendingDelete.id), 'Announcement deleted.')
          if (deleted.ok) setPendingDelete(null)
        }}
      />
    </>
  )
}
