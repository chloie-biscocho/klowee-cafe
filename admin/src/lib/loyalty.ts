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

/** The clubs the customer holds a card in: only these collect stamps (handover 16a). */
export const memberClubs = (clubs: CustomerClubDto[]) => clubs.filter((c) => c.membership === 'member')

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

/** "2026-09-02" → "9/2", the way the owners write it above a stamp. */
export function stampDate(isoDate: string): string {
  const [, month, day] = isoDate.split('-').map(Number)
  return `${month}/${day}`
}

const monthName = new Intl.DateTimeFormat('en-PH', { month: 'long', timeZone: 'UTC' })

/** "2026-09-02" → "2 September", for screen readers (day first, whatever the locale's order). */
export function stampDateLong(isoDate: string): string {
  const [year, month, day] = isoDate.split('-').map(Number)
  return `${day} ${monthName.format(new Date(Date.UTC(year!, month! - 1, 1)))}`
}

/**
 * A small fixed tilt for slot n, from −12 to 12 degrees, so the stamps look
 * pressed by hand but never move between renders: a pure function of the
 * slot number, no randomness. 7 is coprime with 25, so slots 1–12 all differ.
 */
export const stampTilt = (slot: number) => ((slot * 7 + 3) % 25) - 12
