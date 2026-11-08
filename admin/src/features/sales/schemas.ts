import { z } from 'zod'
import { PAYMENT_CHANNELS } from '../../types/api'

/** Mirrors CustomerRequest's annotations; the API still checks. */
export const customerSchema = z.object({
  name: z.string().trim().min(1, 'Name is required.').max(120, 'Name is too long.'),
  nickname: z.string().trim().max(80, 'Keep the nickname under 80 characters.'),
  notes: z.string().trim().max(1000, 'Notes are too long.'),
})
export type CustomerValues = z.infer<typeof customerSchema>

/** A payment, for one order or spread over a tab. */
export const paymentSchema = z.object({
  amount: z.coerce.number<number>().gt(0, 'Enter an amount above zero.').max(1_000_000),
  channel: z.enum(PAYMENT_CHANNELS),
  paidOn: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Pick a date.'),
  reference: z.string().trim().max(120, 'Keep the reference under 120 characters.'),
})
export type PaymentValues = z.infer<typeof paymentSchema>

export const adjustSchema = z.object({
  delta: z.coerce
    .number<number>()
    .int('Stamps are whole numbers.')
    .min(-1000)
    .max(1000)
    .refine((value) => value !== 0, 'Enter a number of stamps to add (or a negative one to remove).'),
  notes: z.string().trim().max(500, 'Keep the note under 500 characters.'),
})
export type AdjustValues = z.infer<typeof adjustSchema>

export const blankToNull = (value: string) => (value.trim() === '' ? null : value.trim())
