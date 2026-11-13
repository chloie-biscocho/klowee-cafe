import type { CustomerClubDto, CustomerRewardDto, LoyaltyMode } from '../types/api'

/** "The Coffee Club" → "Coffee Club". */
export const clubLabel = (name: string) => name.replace(/^the\s+/i, '')

/** "The Coffee Club" → "Coffee", for tight columns. */
export const clubShortName = (name: string) => clubLabel(name).replace(/\s+club$/i, '')

/** A reward as the owners would say it: "50% OFF" → "50% off". */
export const rewardPhrase = (label: string) => label.toLowerCase()

/** The rewards a customer can use now on this club, oldest card first. */
export const availableRewards = (club: CustomerClubDto): CustomerRewardDto[] =>
  club.rewards.filter((r) => r.state === 'available').sort((a, b) => a.cycle - b.cycle || a.slot - b.slot)

/** "Coffee 6/12 · 1 reward" */
export function clubSummary(club: CustomerClubDto): string {
  const rewards = availableRewards(club).length
  const base = `${clubShortName(club.name)} ${club.position}/${club.slotCount}`
  return rewards === 0 ? base : `${base} · ${rewards} reward${rewards === 1 ? '' : 's'}`
}

/**
 * The item form's single "Loyalty card" select: 'inherit', 'none', or a club
 * id, to and from the API's (loyaltyMode, loyaltyClubId) pair.
 */
export const loyaltyChoice = (mode: LoyaltyMode, clubId: string | null): string =>
  mode === 'Club' && clubId ? clubId : mode === 'None' ? 'none' : 'inherit'

export const fromLoyaltyChoice = (choice: string): { loyaltyMode: LoyaltyMode; loyaltyClubId: string | null } =>
  choice === 'inherit'
    ? { loyaltyMode: 'Inherit', loyaltyClubId: null }
    : choice === 'none'
      ? { loyaltyMode: 'None', loyaltyClubId: null }
      : { loyaltyMode: 'Club', loyaltyClubId: choice }
