import { Link } from 'react-router'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Td } from '../../components/ui/Table'
import { formatDate } from '../../lib/format'
import { MENU_CONTEXT_LABELS, type MenuVersionSummaryDto } from '../../types/api'
import { LockIcon } from './LockIcon'

export const PUBLISHED_HINT = "published versions can't be edited; copy it to make changes"

/** One row of the versions table. Drafts get Edit and Delete; published ones do not. */
export function VersionRow({
  version,
  onEdit,
  onTogglePublish,
  onDelete,
}: {
  version: MenuVersionSummaryDto
  onEdit: () => void
  onTogglePublish: () => void
  onDelete: () => void
}) {
  return (
    <tr>
      <Td>
        <Link
          to={`/menu/versions/${version.id}`}
          className="flex items-center gap-1.5 font-medium hover:text-accent hover:underline"
          title={version.isPublished ? PUBLISHED_HINT : undefined}
        >
          {version.isPublished && <LockIcon />}
          {version.name}
        </Link>
      </Td>
      <Td>
        <Badge tone="accent">{MENU_CONTEXT_LABELS[version.context]}</Badge>
      </Td>
      <Td className="text-muted">{formatDate(version.effectiveFrom)}</Td>
      <Td className="text-muted">{version.itemCount}</Td>
      <Td>
        <Badge tone={version.isPublished ? 'success' : 'neutral'}>
          {version.isPublished ? 'Published' : 'Draft'}
        </Badge>
      </Td>
      <Td className="text-right">
        <div className="flex justify-end gap-1.5">
          {!version.isPublished && (
            <Button size="sm" onClick={onEdit}>
              Edit
            </Button>
          )}
          <Button size="sm" onClick={onTogglePublish}>
            {version.isPublished ? 'Unpublish' : 'Publish'}
          </Button>
          {!version.isPublished && (
            <Button size="sm" variant="danger" onClick={onDelete}>
              Delete
            </Button>
          )}
        </div>
      </Td>
    </tr>
  )
}
