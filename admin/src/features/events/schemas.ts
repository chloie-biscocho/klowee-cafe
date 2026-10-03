import { z } from 'zod'
import { EVENT_STATUSES, type EventRequest } from '../../types/api'

const isoDate = (message: string) => z.string().regex(/^\d{4}-\d{2}-\d{2}$/, message)

/** Mirrors `EventRequest`; the date rule repeats `EnsureDatesMakeSense`. */
export const eventSchema = z
  .object({
    name: z.string().trim().min(1, 'Name is required.').max(160, 'Name is too long.'),
    venue: z.string().trim().min(1, 'Venue is required.').max(160, 'Venue is too long.'),
    address: z.string().trim().max(300, 'Address is too long.'),
    startsOn: isoDate('Pick a start date.'),
    endsOn: isoDate('Pick an end date.'),
    status: z.enum(EVENT_STATUSES),
    description: z.string().trim().max(4000, 'Description is too long.'),
    coverPhotoUrl: z.url().nullable(),
  })
  // Plain ISO dates order correctly as strings.
  .refine((values) => values.endsOn >= values.startsOn, {
    path: ['endsOn'],
    message: 'The end date cannot be before the start date.',
  })
export type EventValues = z.infer<typeof eventSchema>

export function toEventRequest(values: EventValues): EventRequest {
  return {
    ...values,
    address: values.address === '' ? null : values.address,
    description: values.description === '' ? null : values.description,
  }
}
