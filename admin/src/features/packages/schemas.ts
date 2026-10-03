import { z } from 'zod'

/** Mirrors `PackageRequest`'s data annotations; the API still checks. */
export const packageSchema = z.object({
  name: z.string().trim().min(1, 'Name is required.').max(120, 'Name is too long.'),
  price: z.coerce.number<number>().min(0, 'Price cannot be negative.').max(1_000_000),
  description: z.string().trim().max(2000, 'Description is too long.'),
  guestCountNote: z.string().trim().max(120, 'Keep the guest note under 120 characters.'),
  sortOrder: z.coerce.number<number>().int('Sort order must be a whole number.').min(0).max(9999),
  isActive: z.boolean(),
  /**
   * Objects, not bare strings, because `useFieldArray` keys each row by an id
   * it adds to an object. Array position is the order; `sortOrder` is derived
   * from it on submit.
   */
  inclusions: z.array(
    z.object({
      text: z.string().trim().min(1, 'Write the inclusion or remove it.').max(300, 'Too long.'),
    }),
  ),
})
export type PackageValues = z.infer<typeof packageSchema>
