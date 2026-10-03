import { zodResolver } from '@hookform/resolvers/zod'
import { useForm, useWatch } from 'react-hook-form'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { Textarea } from '../../components/ui/Textarea'
import { isoToManilaLocal, nowInManilaLocal } from '../../lib/dates'
import type { AnnouncementDto } from '../../types/api'
import { ANNOUNCEMENT_MAX, announcementSchema, type AnnouncementValues } from './schemas'

export function AnnouncementForm({
  announcement,
  saving,
  onClose,
  onSubmit,
}: {
  /** `null` means "new announcement". */
  announcement: AnnouncementDto | null
  saving: boolean
  onClose: () => void
  onSubmit: (values: AnnouncementValues) => void
}) {
  const { register, handleSubmit, control, formState } = useForm<AnnouncementValues>({
    resolver: zodResolver(announcementSchema),
    defaultValues: {
      text: announcement?.text ?? '',
      linkUrl: announcement?.linkUrl ?? '',
      // The API returns UTC; the inputs show Manila wall-clock time.
      activeFrom: announcement ? isoToManilaLocal(announcement.activeFrom) : nowInManilaLocal(),
      activeUntil: announcement?.activeUntil ? isoToManilaLocal(announcement.activeUntil) : '',
      isActive: announcement?.isActive ?? true,
    },
  })
  const { errors } = formState
  const length = useWatch({ control, name: 'text' }).length

  return (
    <Modal
      open
      title={announcement ? 'Edit announcement' : 'New announcement'}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button variant="primary" loading={saving} onClick={handleSubmit(onSubmit)}>
            Save
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <div>
          <Textarea
            label="Text"
            autoFocus
            maxLength={ANNOUNCEMENT_MAX}
            error={errors.text?.message}
            {...register('text')}
          />
          <p
            aria-live="polite"
            className={`mt-1 text-right text-xs ${length >= ANNOUNCEMENT_MAX ? 'text-danger' : 'text-muted'}`}
          >
            {length} / {ANNOUNCEMENT_MAX}
          </p>
        </div>
        <Input
          label="Link URL"
          placeholder="https://instagram.com/…"
          hint="Optional. Where the ticker links to."
          error={errors.linkUrl?.message}
          {...register('linkUrl')}
        />
        <div className="grid grid-cols-2 gap-3">
          <Input
            label="Active from"
            type="datetime-local"
            hint="Manila time."
            error={errors.activeFrom?.message}
            {...register('activeFrom')}
          />
          <Input
            label="Active until"
            type="datetime-local"
            hint="Manila time. Leave empty for open-ended."
            error={errors.activeUntil?.message}
            {...register('activeUntil')}
          />
        </div>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" className="size-4 accent-accent" {...register('isActive')} />
          Active
        </label>
      </div>
    </Modal>
  )
}
