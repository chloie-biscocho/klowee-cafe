import { Link } from 'react-router'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { formatDateRange } from '../../lib/format'
import type { EventDetailDto } from '../../types/api'
import { canPublish, EVENT_STATUS_TONES, PUBLISH_BLOCKED_HINT } from './eventStatus'

interface EventDetailHeaderProps {
  event: EventDetailDto
  isPublishing: boolean
  onEdit: () => void
  onTogglePublish: () => void
  onDelete: () => void
}

export function EventDetailHeader({
  event,
  isPublishing,
  onEdit,
  onTogglePublish,
  onDelete,
}: EventDetailHeaderProps) {
  const publishBlocked = !event.isPublished && !canPublish(event.status)

  return (
    <div className="mb-4 flex flex-wrap items-start justify-between gap-4">
      <div>
        <Link to="/site/events" className="text-xs text-muted hover:text-ink hover:underline">
          &larr; All events
        </Link>
        <h2 className="mt-1 text-lg font-bold tracking-tight">{event.name}</h2>
        <div className="mt-1.5 flex flex-wrap items-center gap-2 text-sm text-muted">
          <span>{event.venue}</span>
          <span>&middot;</span>
          <span>{formatDateRange(event.startsOn, event.endsOn)}</span>
          <Badge tone={EVENT_STATUS_TONES[event.status]}>{event.status}</Badge>
          <Badge tone={event.isPublished ? 'success' : 'neutral'}>
            {event.isPublished ? 'Published' : 'Hidden'}
          </Badge>
        </div>
      </div>

      <div className="flex items-center gap-2">
        <Button onClick={onEdit}>Edit details</Button>
        {/* A disabled button gets no hover events in some browsers, so the
            tooltip sits on a wrapper. */}
        <span title={publishBlocked ? PUBLISH_BLOCKED_HINT : undefined}>
          <Button
            variant={event.isPublished ? 'secondary' : 'primary'}
            loading={isPublishing}
            disabled={publishBlocked}
            onClick={onTogglePublish}
          >
            {event.isPublished ? 'Unpublish' : 'Publish'}
          </Button>
        </span>
        <Button variant="danger" onClick={onDelete}>
          Delete
        </Button>
      </div>
    </div>
  )
}
