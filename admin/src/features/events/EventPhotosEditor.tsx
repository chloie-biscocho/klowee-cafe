import { useState } from 'react'
import { useReplaceEventPhotosMutation } from '../../api/eventsApi'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { ImageUpload } from '../../components/ui/ImageUpload'
import { UnsavedChangesPrompt } from '../../components/ui/UnsavedChangesPrompt'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import { moveItem } from '../../lib/lists'
import type { EventPhotoDto, EventPhotoRequest } from '../../types/api'

/** A photo while being edited. The caption is a string because an input holds one. */
interface EditorPhoto {
  url: string
  caption: string
}

function toEditorPhotos(photos: EventPhotoDto[]): EditorPhoto[] {
  return photos.map((photo) => ({ url: photo.url, caption: photo.caption ?? '' }))
}

/** The body for `PUT /events/{id}/photos`: the whole list, position as `sortOrder`. */
function toRequestPhotos(photos: EditorPhoto[]): EventPhotoRequest[] {
  return photos.map((photo, index) => ({
    url: photo.url,
    caption: photo.caption.trim() === '' ? null : photo.caption.trim(),
    sortOrder: index,
  }))
}

export function EventPhotosEditor({ eventId, photos }: { eventId: string; photos: EventPhotoDto[] }) {
  const run = useToastedAction()
  const [replacePhotos, { isLoading: isSaving }] = useReplaceEventPhotosMutation()

  /**
   * The version editor's pattern: `null` renders straight from the cache, the
   * first edit takes a copy, a successful save drops back to `null`.
   */
  const [draft, setDraft] = useState<EditorPhoto[] | null>(null)
  const items = draft ?? toEditorPhotos(photos)
  const isDirty = draft !== null

  const onSave = async () => {
    const saved = await run(
      replacePhotos({ id: eventId, photos: toRequestPhotos(items) }),
      'Photos saved.',
    )
    if (saved.ok) setDraft(null)
  }

  return (
    <Card>
      <CardHeader>
        <p className="text-sm text-muted">
          Shown in this order in the event's gallery on the public site.
        </p>
        <Button variant="primary" size="sm" loading={isSaving} disabled={!isDirty} onClick={onSave}>
          {isDirty ? 'Save photos' : 'Saved'}
        </Button>
      </CardHeader>

      <ul className="grid grid-cols-[repeat(auto-fill,minmax(200px,1fr))] gap-4 p-4">
        {items.map((photo, index) => (
          <li key={`${photo.url}-${index}`} className="flex flex-col gap-2">
            <img
              src={photo.url}
              alt={photo.caption || `Photo ${index + 1}`}
              className="h-28 w-full rounded-md border border-line object-cover"
            />
            <input
              aria-label={`Caption for photo ${index + 1}`}
              placeholder="Caption (optional)"
              value={photo.caption}
              maxLength={300}
              onChange={(event) =>
                setDraft(
                  items.map((item, at) =>
                    at === index ? { ...item, caption: event.target.value } : item,
                  ),
                )
              }
              className="h-8 w-full rounded-md border border-line bg-surface px-2 text-sm"
            />
            <div className="flex gap-1">
              <Button
                size="sm"
                variant="ghost"
                aria-label={`Move photo ${index + 1} earlier`}
                disabled={index === 0}
                onClick={() => setDraft(moveItem(items, index, -1))}
              >
                &larr;
              </Button>
              <Button
                size="sm"
                variant="ghost"
                aria-label={`Move photo ${index + 1} later`}
                disabled={index === items.length - 1}
                onClick={() => setDraft(moveItem(items, index, 1))}
              >
                &rarr;
              </Button>
              <Button
                size="sm"
                variant="danger"
                className="ml-auto"
                onClick={() => setDraft(items.filter((_, at) => at !== index))}
              >
                Remove
              </Button>
            </div>
          </li>
        ))}
        <li>
          {/* Always empty: each upload appends a photo instead of filling this slot. */}
          <ImageUpload
            compact
            label="Add photo"
            folder="events"
            value={null}
            onChange={(url) => url && setDraft([...items, { url, caption: '' }])}
          />
        </li>
      </ul>

      <UnsavedChangesPrompt
        when={isDirty}
        message="This event has unsaved photo changes. They will be lost."
      />
    </Card>
  )
}
