import { Link } from 'react-router'
import { rewardPhrase, stampDate, stampDateLong, stampTilt } from '../../../lib/loyalty'
import type { CustomerRewardDto, LoyaltyCardDto, LoyaltySlotDto } from '../../../types/api'
import { StampMark } from './StampMark'

const UNDATED_TITLE: Record<string, string> = {
  carriedOver: 'From the paper card',
  adjustment: 'Added by hand',
}

/** The paper card's colours: the free drink's slot blue, the other reward slots pink. */
function tint(reward: CustomerRewardDto | undefined) {
  if (!reward) return 'border-line bg-surface'
  return reward.type === 'FreeDrink' ? 'border-stamp-blue-edge/40 bg-stamp-blue' : 'border-stamp-pink-edge/30 bg-stamp-pink'
}

function slotLabel(slot: LoyaltySlotDto, reward: CustomerRewardDto | undefined) {
  const what = !slot.filled
    ? 'empty'
    : slot.date
      ? `stamped ${stampDateLong(slot.date)}`
      : `stamped, ${UNDATED_TITLE[slot.source ?? '']?.toLowerCase() ?? 'undated'}`
  return `Slot ${slot.slot}, ${what}${reward ? `, ${reward.label} ${reward.state}` : ''}`
}

/**
 * One card as the owners keep it on paper (handover 16b): two rows of six,
 * the date above each stamped slot, a cup inside it, reward slots tinted.
 * A stamp from an order links to that order.
 */
export function StampCard({ card }: { card: LoyaltyCardDto }) {
  const rewardOn = new Map(card.rewards.map((r) => [r.slot, r]))
  const filled = card.slots.filter((s) => s.filled)
  const fromPaper = filled.length > 0 && filled.every((s) => s.source === 'carriedOver')

  return (
    <div className="flex flex-col gap-2">
      {fromPaper && <p className="text-xs text-muted">from paper card</p>}
      <ol aria-label={`Card ${card.cardNumber}: ${filled.length} of ${card.slots.length} slots stamped`} className="grid grid-cols-6 gap-x-2 gap-y-3">
        {card.slots.map((slot) => {
          const reward = rewardOn.get(slot.slot)
          const label = slotLabel(slot, reward)
          const circle = (
            <span
              className={`flex size-11 items-center justify-center rounded-full border text-[10px] leading-tight ${tint(reward)} ${
                slot.filled ? 'text-accent' : 'text-muted'
              }`}
            >
              {slot.filled ? (
                <span className="inline-flex" style={{ transform: `rotate(${stampTilt(slot.slot)}deg)` }}>
                  <StampMark className="size-6" />
                </span>
              ) : reward ? (
                <span className="px-0.5 text-center">{rewardPhrase(reward.label)}</span>
              ) : (
                <span className="tabular-nums">{slot.slot}</span>
              )}
            </span>
          )
          return (
            <li key={slot.slot} className="flex flex-col items-center gap-0.5">
              <span aria-hidden="true" className="h-4 text-[11px] text-muted tabular-nums">
                {slot.date ? stampDate(slot.date) : ''}
              </span>
              {slot.filled && slot.orderId ? (
                <Link to={`/sales/orders/${slot.orderId}`} aria-label={label} className="rounded-full">
                  {circle}
                </Link>
              ) : (
                <span role="img" aria-label={label} title={slot.filled ? UNDATED_TITLE[slot.source ?? ''] : undefined}>
                  {circle}
                </span>
              )}
            </li>
          )
        })}
      </ol>
    </div>
  )
}
