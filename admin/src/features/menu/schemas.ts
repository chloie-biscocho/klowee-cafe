import { z } from 'zod'
import { MENU_CONTEXTS } from '../../types/api'

/**
 * One schema per form, all in one file because they are small and always read
 * together. They mirror the data annotations on the API's request records, so
 * the browser rejects what the server would reject anyway — the server still
 * checks, this only saves a round trip and gives a better message.
 */

const requiredText = (label: string, max: number) =>
  z.string().trim().min(1, `${label} is required.`).max(max, `${label} is too long.`)

export const categorySchema = z.object({
  name: requiredText('Name', 80),
  sortOrder: z.coerce.number<number>().int('Sort order must be a whole number.').min(0).max(9999),
})
export type CategoryValues = z.infer<typeof categorySchema>

export const itemSchema = z.object({
  name: requiredText('Name', 120),
  description: z.string().trim().max(1000, 'Description is too long.'),
  categoryId: z.uuid('Pick a category.'),
  // The API validates this with [Url], so an empty box has to mean "no photo".
  photoUrl: z.union([z.literal(''), z.url('Enter a full URL, including https://')]),
})
export type ItemValues = z.infer<typeof itemSchema>

export const addOnSchema = z.object({
  name: requiredText('Name', 80),
  price: z.coerce.number<number>().min(0, 'Price cannot be negative.').max(100000),
  isActive: z.boolean(),
})
export type AddOnValues = z.infer<typeof addOnSchema>

const isoDate = z
  .string()
  .regex(/^\d{4}-\d{2}-\d{2}$/, 'Pick an effective date.')

export const versionSchema = z.object({
  name: requiredText('Name', 120),
  context: z.enum(MENU_CONTEXTS),
  effectiveFrom: isoDate,
  notes: z.string().trim().max(2000, 'Notes are too long.'),
  /** Empty string means "start from nothing". */
  copyFromVersionId: z.string(),
})
export type VersionValues = z.infer<typeof versionSchema>
