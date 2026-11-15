import { Link } from 'react-router'
import { stampDate, stampDateLong } from '../../../lib/loyalty'
import type { CustomerRewardDto, LoyaltyCardDto, LoyaltyClubStyle, LoyaltySlotDto } from '../../../types/api'
import { StampMark } from './StampMark'
import './card.css'

const UNDATED_TITLE: Record<string, string> = {
  carriedOver: 'From the paper card',
  adjustment: 'Added by hand',
}

function slotLabel(slot: LoyaltySlotDto, reward: CustomerRewardDto | undefined) {
  const what = !slot.filled
    ? 'empty'
    : slot.date
      ? `stamped ${stampDateLong(slot.date)}`
      : `stamped, ${UNDATED_TITLE[slot.source ?? '']?.toLowerCase() ?? 'undated'}`
  return `Slot ${slot.slot}, ${what}${reward ? `, ${reward.label} ${reward.state}` : ''}`
}

/** The stamp printed in a plain slot: a bean heart for coffee, a leaf for matcha (the client's SlotIcon). */
function SlotIcon({ style }: { style: LoyaltyClubStyle }) {
  return style === 'Matcha' ? (
    <svg viewBox="0 0 64 56" aria-hidden="true" className="absolute top-[20%] left-[17%] h-[60%] w-[66%]">
      <path className="fill-leaf" d="M32 3C15 13 8 29 14 50c19 3 35-8 39-30C50 11 42 5 32 3z" />
      <path className="stroke-card" d="M18 46C26 34 35 24 46 13" fill="none" strokeWidth="2.8" strokeLinecap="round" />
    </svg>
  ) : (
    <svg viewBox="0 0 64 56" aria-hidden="true" className="absolute top-[20%] left-[17%] h-[60%] w-[66%]">
      <g className="fill-bean">
        <ellipse cx="22" cy="28" rx="15" ry="23" transform="rotate(-32 22 28)" />
        <ellipse cx="42" cy="28" rx="15" ry="23" transform="rotate(32 42 28)" />
      </g>
      <g className="stroke-card" fill="none" strokeWidth="2.6" strokeLinecap="round">
        <path d="M14 12c8 8 2 20 14 32" />
        <path d="M50 12c-8 8-2 20-14 32" />
      </g>
    </svg>
  )
}

/** The three hand-drawn squiggles in the printed card's corners (the client's Squiggles). */
function Squiggles() {
  const common = { viewBox: '0 0 24 24', fill: 'none', strokeWidth: 3, strokeLinecap: 'round' as const, 'aria-hidden': true }
  return (
    <>
      <svg {...common} className="absolute top-2 left-2 size-[26px] stroke-squiggle-red opacity-90">
        <path d="M17 3c-6-1-9 5-4 6s-7 3-3 6 6-1 2 5" />
      </svg>
      <svg {...common} className="absolute top-1.5 right-2 size-[26px] stroke-squiggle-orange opacity-90">
        <path d="M4 5c8-3 12 6 5 7s8 0 11 6" />
      </svg>
      <svg {...common} className="absolute right-2.5 bottom-2 size-[26px] stroke-squiggle-teal opacity-90">
        <path d="M18 3c1 9-3 15-9 15s-2-8 3-5" />
      </svg>
    </>
  )
}

/** The club's name in capitals, its accent letter (if any) in pink, as the paper card prints it (the client's ClubName). */
export function ClubName({ name, accent }: { name: string; accent: number | null }) {
  const upper = name.toUpperCase()
  if (accent === null || accent < 0 || accent >= upper.length || upper.length !== name.length) return <>{upper}</>
  return (
    <>
      {upper.slice(0, accent)}
      <span data-accent className="text-accent-pink">
        {upper[accent]}
      </span>
      {upper.slice(accent + 1)}
    </>
  )
}

/** A reward's words on two lines, as printed: "50%" / "OFF". */
const printed = (label: string) => label.toUpperCase().replace(' ', '\n')

/**
 * One card as the customer sees it on their tab page (handover 18, part 3;
 * the client's CardFace, ported): the cream printed card, the club name in
 * Bowlby One with its pink letter, a bean heart or leaf in every plain slot,
 * reward slots pink and the last in the club colour, and a SIPPED or CLAIMED
 * stamp over each filled slot with its M/D date above. What the admin adds:
 * a stamp from an order links to that order, and undated stamps keep their
 * titles ("From the paper card", "Added by hand").
 */
export function StampCard({
  card,
  clubName,
  style,
  accentLetter = null,
  next = null,
}: {
  card: LoyaltyCardDto
  clubName: string
  style: LoyaltyClubStyle
  accentLetter?: number | null
  /** The slot the next drink fills, ringed in the club colour (the current card only). */
  next?: number | null
}) {
  const rewardOn = new Map(card.rewards.map((r) => [r.slot, r]))
  const lastRewardSlot = Math.max(0, ...card.rewards.map((r) => r.slot))
  const filled = card.slots.filter((s) => s.filled)
  const fromPaper = filled.length > 0 && filled.every((s) => s.source === 'carriedOver')
  const matcha = style === 'Matcha'
  const clubText = matcha ? 'text-matcha' : 'text-cobalt'
  const clubBg = matcha ? 'bg-matcha' : 'bg-cobalt'
  const clubRing = matcha ? 'shadow-[inset_0_0_0_2.5px_var(--color-matcha)]' : 'shadow-[inset_0_0_0_2.5px_var(--color-cobalt)]'

  return (
    <div className="flex flex-col gap-2">
      <div className="relative overflow-hidden rounded-[18px] bg-card px-3 pt-[18px] pb-4 text-center text-card-ink">
        <Squiggles />
        {fromPaper && <p className="mb-1 text-xs font-bold text-card-ink">from paper card</p>}
        <p aria-hidden="true" className="px-5 font-hand text-[0.62rem] font-medium tracking-[.12em]">
          KLOWEE CAFE'S LOYALTY CARD PRESENTS
        </p>
        <p aria-hidden="true" className={`mt-0.5 mb-3 font-display text-[1.6rem] leading-[1.05] tracking-[.01em] ${clubText}`}>
          <ClubName name={clubName} accent={accentLetter} />
        </p>
        <ol
          aria-label={`Card ${card.cardNumber}: ${filled.length} of ${card.slots.length} slots stamped`}
          className="mx-auto grid max-w-[520px] gap-x-1.5 gap-y-1.5"
          style={{ gridTemplateColumns: `repeat(${Math.ceil(card.slots.length / 2)}, minmax(0, 1fr))` }}
        >
          {card.slots.map((slot) => {
            const reward = rewardOn.get(slot.slot)
            const isLast = reward?.slot === lastRewardSlot
            const ready = reward?.state === 'available'
            const label = slotLabel(slot, reward)
            const circle = (
              <span
                className={`relative grid aspect-square w-full place-content-center rounded-full ${
                  reward ? (isLast ? clubBg : 'bg-card-pink') : 'bg-card-slot'
                } ${ready ? 'shadow-[0_0_0_3px_var(--color-yellow)]' : next === slot.slot ? clubRing : ''}`}
              >
                {reward ? (
                  <span
                    className={`px-0.5 font-hand text-[0.62rem] leading-[1.1] font-bold tracking-[.04em] whitespace-pre-line ${
                      // Dark ink on the pink (white measured 2.7:1); cream on the club colour.
                      isLast ? 'text-card' : 'text-card-ink'
                    }`}
                  >
                    {printed(reward.label)}
                  </span>
                ) : (
                  <SlotIcon style={style} />
                )}
                {slot.filled && <StampMark slot={slot.slot} claimed={reward?.state === 'used'} onClubColour={isLast} matcha={matcha} />}
              </span>
            )
            return (
              <li key={slot.slot} className="flex flex-col items-center">
                <span aria-hidden="true" className="mb-0.5 h-[1.1em] font-sans text-[0.72rem] leading-[1.1] font-bold text-card-ink tabular-nums">
                  {slot.date ? stampDate(slot.date) : ''}
                </span>
                {slot.filled && slot.orderId ? (
                  <Link to={`/sales/orders/${slot.orderId}`} aria-label={label} className="block w-full rounded-full">
                    {circle}
                  </Link>
                ) : (
                  <span role="img" aria-label={label} title={slot.filled ? UNDATED_TITLE[slot.source ?? ''] : undefined} className="block w-full">
                    {circle}
                  </span>
                )}
              </li>
            )
          })}
        </ol>
        <p aria-hidden="true" className="mt-3 text-[0.68rem] font-bold tracking-[.08em]">
          COLLECT THE STAMP AND GET A FREEBIE!
        </p>
      </div>
    </div>
  )
}
