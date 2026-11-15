import { Link } from 'react-router'
import { useGetLoyaltyQuery } from '../../../api/salesApi'
import { formatDate } from '../../../lib/format'
import type { CustomerClubDto, LoyaltySlotDto } from '../../../types/api'

/** How many stamps the details column lists: three, not five, so the column stays near the card's height (handover 18a). */
export const RECENT_STAMPS = 3

/** The carry-over's own adjustments: they fill "from the paper card" slots, not hand ones. */
const CARRY_OVER_NOTES = new Set(['carried over from paper card', 'carried over from the old card'])

interface Stamp {
  card: number
  slot: LoyaltySlotDto
  reason: string | null
}

/**
 * The latest stamps on a club, newest first (handover 18a): its date, and the
 * order it came from, or "from the paper card", or "added by hand" with the
 * reason given. Built from the card slots the page already has; the reasons
 * come from the ledger (the same query as the Loyalty ledger tab). Hand
 * stamps are paired with the hand adjustments oldest first, the order the API
 * lays slots out in; when the counts don't line up (a stamp taken away later,
 * the ledger still loading), the reason is left out rather than guessed.
 */
export function RecentStamps({ customerId, club }: { customerId: string; club: CustomerClubDto }) {
  const { data: ledger } = useGetLoyaltyQuery({ id: customerId, clubId: club.clubId })

  const filled = [...club.cards]
    .sort((a, b) => a.cardNumber - b.cardNumber)
    .flatMap((card) => card.slots.filter((s) => s.filled).map((slot) => ({ card: card.cardNumber, slot })))
  if (filled.length === 0) return null

  // The reason for each hand stamp, oldest first, one per stamp.
  const handSlots = filled.filter((f) => f.slot.source === 'adjustment')
  const handReasons = [...(ledger ?? [])]
    .filter((e) => e.reason === 'Adjustment' && e.delta > 0 && !e.orderId && !CARRY_OVER_NOTES.has(e.notes ?? ''))
    .sort((a, b) => a.createdAt.localeCompare(b.createdAt) || a.id.localeCompare(b.id))
    .flatMap((e) => Array.from({ length: e.delta }, () => e.notes))
  const paired = handReasons.length === handSlots.length

  const stamps: Stamp[] = filled.map((f) => ({
    ...f,
    reason: f.slot.source === 'adjustment' && paired ? (handReasons[handSlots.indexOf(f)] ?? null) : null,
  }))
  const recent = stamps.slice(-RECENT_STAMPS).reverse()

  return (
    <div className="order-4 wide:order-none">
      <h4 className="mb-1.5 text-[0.8rem] font-bold text-muted">Recent stamps</h4>
      <ul aria-label="Recent stamps" className="flex flex-col divide-y-[1.5px] divide-line rounded-2xl border-[1.5px] border-line text-sm">
        {recent.map(({ card, slot, reason }) => (
          <li key={`${card}-${slot.slot}`} className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5 px-3.5 py-2">
            <span>
              <span className="tabular-nums">{slot.date ? formatDate(slot.date) : 'No date'}</span>
              <span className="text-muted">
                {' '}
                · card {card}, slot {slot.slot}
              </span>
            </span>
            <span className="text-right">
              {slot.source === 'order' && slot.orderId ? (
                <Link className="font-bold text-accent hover:underline" to={`/sales/orders/${slot.orderId}`}>
                  order
                </Link>
              ) : slot.source === 'carriedOver' ? (
                <span className="text-muted">from the paper card</span>
              ) : (
                <span className="text-muted">
                  added by hand{reason ? `: ${reason}` : ''}
                </span>
              )}
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}
