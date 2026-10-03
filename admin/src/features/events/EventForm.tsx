import { zodResolver } from '@hookform/resolvers/zod'
import { Controller, useForm } from 'react-hook-form'
import { Button } from '../../components/ui/Button'
import { ImageUpload } from '../../components/ui/ImageUpload'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { Select } from '../../components/ui/Select'
import { Textarea } from '../../components/ui/Textarea'
import { todayInManila } from '../../lib/dates'
import { EVENT_STATUSES, type EventDetailDto } from '../../types/api'
import { eventSchema, type EventValues } from './schemas'

/** The core fields, for both "New event" and "Edit details". Photos live on the detail page. */
export function EventForm({
  event,
  saving,
  onClose,
  onSubmit,
}: {
  /** `null` means "new event". */
  event: EventDetailDto | null
  saving: boolean
  onClose: () => void
  onSubmit: (values: EventValues) => void
}) {
  const today = todayInManila()
  const { register, handleSubmit, control, formState } = useForm<EventValues>({
    resolver: zodResolver(eventSchema),
    defaultValues: {
      name: event?.name ?? '',
      venue: event?.venue ?? '',
      address: event?.address ?? '',
      startsOn: event?.startsOn ?? today,
      endsOn: event?.endsOn ?? today,
      status: event?.status ?? 'Planned',
      description: event?.description ?? '',
      coverPhotoUrl: event?.coverPhotoUrl ?? null,
    },
  })
  const { errors } = formState

  return (
    <Modal
      open
      title={event ? 'Edit event details' : 'New event'}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button variant="primary" loading={saving} onClick={handleSubmit(onSubmit)}>
            {event ? 'Save' : 'Create event'}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <Input label="Name" autoFocus error={errors.name?.message} {...register('name')} />
        <div className="grid grid-cols-2 gap-3">
          <Input label="Venue" error={errors.venue?.message} {...register('venue')} />
          <Select label="Status" error={errors.status?.message} {...register('status')}>
            {EVENT_STATUSES.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </Select>
        </div>
        <Input label="Address" error={errors.address?.message} {...register('address')} />
        <div className="grid grid-cols-2 gap-3">
          <Input
            label="Starts on"
            type="date"
            error={errors.startsOn?.message}
            {...register('startsOn')}
          />
          <Input
            label="Ends on"
            type="date"
            hint="Same as the start for a one-day event."
            error={errors.endsOn?.message}
            {...register('endsOn')}
          />
        </div>
        <Textarea
          label="Description"
          rows={4}
          error={errors.description?.message}
          {...register('description')}
        />
        <Controller
          control={control}
          name="coverPhotoUrl"
          render={({ field }) => (
            <ImageUpload
              label="Cover image"
              folder="events"
              value={field.value}
              onChange={field.onChange}
            />
          )}
        />
      </div>
    </Modal>
  )
}
