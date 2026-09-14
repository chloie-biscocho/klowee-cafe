import { useNavigate } from 'react-router'
import { useCreateVersionMutation, useListVersionsQuery } from '../../api/menuApi'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import type { MenuVersionDetailDto } from '../../types/api'
import { VersionFormModal } from './VersionFormModal'

/**
 * "Copy to new draft" from inside a published version: the same create form,
 * pre-filled to copy this version's items, and it opens the new draft on success.
 */
export function CopyVersionModal({
  version,
  onClose,
}: {
  version: MenuVersionDetailDto
  onClose: () => void
}) {
  const navigate = useNavigate()
  const run = useToastedAction()
  const { data: versions } = useListVersionsQuery({})
  const [createVersion, { isLoading }] = useCreateVersionMutation()

  return (
    <VersionFormModal
      open
      editing={null}
      versions={versions ?? []}
      initial={{
        name: `${version.name} (copy)`,
        context: version.context,
        copyFromVersionId: version.id,
      }}
      saving={isLoading}
      onClose={onClose}
      onSubmit={async (values) => {
        const created = await run(
          createVersion({
            name: values.name,
            context: values.context,
            effectiveFrom: values.effectiveFrom,
            notes: values.notes === '' ? null : values.notes,
            copyFromVersionId: values.copyFromVersionId === '' ? null : values.copyFromVersionId,
          }),
          'Draft created from this version.',
        )
        if (created.ok) {
          onClose()
          await navigate(`/menu/versions/${created.data.id}`)
        }
      }}
    />
  )
}
