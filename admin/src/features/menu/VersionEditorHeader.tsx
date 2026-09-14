import { Link } from 'react-router'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { formatDate } from '../../lib/format'
import { MENU_CONTEXT_LABELS, type MenuVersionDetailDto } from '../../types/api'
import { LockIcon } from './LockIcon'

interface VersionEditorHeaderProps {
  version: MenuVersionDetailDto
  isDirty: boolean
  isSaving: boolean
  isPublishing: boolean
  onSave: () => void
  onPublish: () => void
  onCopy: () => void
}

export function VersionEditorHeader({
  version,
  isDirty,
  isSaving,
  isPublishing,
  onSave,
  onPublish,
  onCopy,
}: VersionEditorHeaderProps) {
  return (
    <div className="mb-4 flex flex-wrap items-start justify-between gap-4">
      <div>
        <Link to="/menu/versions" className="text-xs text-muted hover:text-ink hover:underline">
          &larr; All versions
        </Link>
        <h2 className="mt-1 flex items-center gap-2 text-lg font-bold tracking-tight">
          {version.isPublished && <LockIcon />}
          {version.name}
        </h2>
        <div className="mt-1.5 flex flex-wrap items-center gap-2 text-sm text-muted">
          <Badge tone="accent">{MENU_CONTEXT_LABELS[version.context]}</Badge>
          <span>effective {formatDate(version.effectiveFrom)}</span>
          <Badge tone={version.isPublished ? 'success' : 'neutral'}>
            {version.isPublished ? 'Published' : 'Draft'}
          </Badge>
        </div>
      </div>

      <div className="flex items-center gap-2">
        {version.isPublished ? (
          <Button variant="primary" onClick={onCopy}>
            Copy to new draft
          </Button>
        ) : (
          <>
            <Button onClick={onSave} loading={isSaving} disabled={!isDirty}>
              {isDirty ? 'Save changes' : 'Saved'}
            </Button>
            <Button
              variant="primary"
              onClick={onPublish}
              loading={isPublishing}
              disabled={isDirty}
              title={isDirty ? 'Save your changes before publishing.' : undefined}
            >
              Publish
            </Button>
          </>
        )}
      </div>
    </div>
  )
}
