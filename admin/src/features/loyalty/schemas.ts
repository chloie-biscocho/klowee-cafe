import { z } from 'zod'
import { LOYALTY_CLUB_STYLES, LOYALTY_REWARD_TYPES, type LoyaltyClubDto, type LoyaltyClubRequest } from '../../types/api'

/**
 * The loyalty card form. Mirrors the API's checks (LoyaltyClubService) so the
 * owner hears about a bad reward before saving; the API still checks.
 */
export const clubSchema = z
  .object({
    name: z.string().trim().min(1, 'Name is required.').max(80, 'Name is too long.'),
    style: z.enum(LOYALTY_CLUB_STYLES),
    slotCount: z.coerce.number<number>().int('Slots are a whole number.').min(1).max(100),
    isActive: z.boolean(),
    /** '' for none: a club whose members get this card automatically. */
    autoJoinFromClubId: z.string(),
    rewards: z.array(
      z.object({
        slot: z.coerce.number<number>().int('Slot is a whole number.').min(1, 'Slot 1 or more.'),
        type: z.enum(LOYALTY_REWARD_TYPES),
        percent: z.string(),
        addOnId: z.string(),
        label: z.string().trim().min(1, 'Label is required.').max(40, 'Keep the label short.'),
      }),
    ),
    categoryIds: z.array(z.string()),
  })
  .superRefine((club, ctx) => {
    const seen = new Set<number>()
    club.rewards.forEach((reward, index) => {
      if (reward.slot > club.slotCount)
        ctx.addIssue({ code: 'custom', path: ['rewards', index, 'slot'], message: `Past the card's ${club.slotCount} slots.` })
      if (seen.has(reward.slot))
        ctx.addIssue({ code: 'custom', path: ['rewards', index, 'slot'], message: 'Another reward is on this slot.' })
      seen.add(reward.slot)
      const percent = Number(reward.percent)
      if (reward.type === 'PercentOff' && !(percent > 0 && percent <= 100))
        ctx.addIssue({ code: 'custom', path: ['rewards', index, 'percent'], message: 'A percent from 1 to 100.' })
      if (reward.type === 'FreeAddOn' && reward.addOnId === '')
        ctx.addIssue({ code: 'custom', path: ['rewards', index, 'addOnId'], message: 'Choose the add-on.' })
    })
  })

export type ClubInput = z.input<typeof clubSchema>
export type ClubValues = z.infer<typeof clubSchema>

export const clubDefaults = (club: LoyaltyClubDto | null): ClubInput => ({
  name: club?.name ?? '',
  style: club?.style ?? 'Coffee',
  slotCount: club?.slotCount ?? 12,
  isActive: club?.isActive ?? false,
  autoJoinFromClubId: club?.autoJoinFromClubId ?? '',
  rewards: (club?.rewards ?? []).map((r) => ({
    slot: r.slot,
    type: r.type,
    percent: r.percent == null ? '' : String(r.percent),
    addOnId: r.addOnId ?? '',
    label: r.label,
  })),
  categoryIds: club?.categoryIds ?? [],
})

export const toClubRequest = (values: ClubValues, sortOrder: number): LoyaltyClubRequest => ({
  name: values.name,
  style: values.style,
  slotCount: values.slotCount,
  isActive: values.isActive,
  sortOrder,
  autoJoinFromClubId: values.autoJoinFromClubId === '' ? null : values.autoJoinFromClubId,
  rewards: values.rewards
    .map((r) => ({
      slot: r.slot,
      type: r.type,
      percent: r.type === 'PercentOff' ? Number(r.percent) : null,
      addOnId: r.type === 'FreeAddOn' ? r.addOnId : null,
      label: r.label,
    }))
    .sort((a, b) => a.slot - b.slot),
  categoryIds: values.categoryIds,
})
