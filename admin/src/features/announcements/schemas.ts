import { z } from 'zod'
import { manilaLocalToIso } from '../../lib/dates'
import type { AnnouncementRequest } from '../../types/api'

/** The ticker is one line on a phone; the API allows 500, the owners asked for 200. */
export const ANNOUNCEMENT_MAX = 200

const LOCAL_DATE_TIME = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/

/**
 * Times are kept exactly as `<input type="datetime-local">` gives them —
 * "2026-05-21T20:00", Manila wall-clock — and converted to an instant only in
 * `toAnnouncementRequest`. Strings of that shape also order correctly, which is
 * all the window check needs.
 */
export const announcementSchema = z
  .object({
    text: z
      .string()
      .trim()
      .min(1, 'Write the announcement.')
      .max(ANNOUNCEMENT_MAX, `Keep it to ${ANNOUNCEMENT_MAX} characters.`),
    linkUrl: z.union([z.literal(''), z.url('Enter a full URL, including https://')]),
    activeFrom: z.string().regex(LOCAL_DATE_TIME, 'Pick when it starts.'),
    activeUntil: z.union([z.literal(''), z.string().regex(LOCAL_DATE_TIME)]),
    isActive: z.boolean(),
  })
  .refine((values) => values.activeUntil === '' || values.activeUntil > values.activeFrom, {
    path: ['activeUntil'],
    message: 'The end must be after the start.',
  })
export type AnnouncementValues = z.infer<typeof announcementSchema>

export function toAnnouncementRequest(values: AnnouncementValues): AnnouncementRequest {
  return {
    text: values.text,
    linkUrl: values.linkUrl === '' ? null : values.linkUrl,
    activeFrom: manilaLocalToIso(values.activeFrom),
    activeUntil: values.activeUntil === '' ? null : manilaLocalToIso(values.activeUntil),
    isActive: values.isActive,
  }
}

/**
 * Is this the kind of announcement the site would show right now? The API's
 * window rule (`AnnouncementService.GetCurrentAsync`): active, started, and
 * not yet ended. Instants compare the same in every zone, so "now in Manila"
 * is simply now.
 */
export function isLiveNow(
  announcement: { isActive: boolean; activeFrom: string; activeUntil: string | null },
  now: Date = new Date(),
): boolean {
  const at = now.getTime()
  return (
    announcement.isActive &&
    Date.parse(announcement.activeFrom) <= at &&
    (announcement.activeUntil === null || Date.parse(announcement.activeUntil) > at)
  )
}
