import { useId, useRef, useState, type KeyboardEvent } from 'react'
import { Card } from '../../../components/ui/Card'
import { clubLabel } from '../../../lib/loyalty'
import type { CustomerClubDto } from '../../../types/api'
import { ClubCard } from './ClubCard'

const tabLabel = (club: CustomerClubDto) =>
  club.membership === 'member' ? `${clubLabel(club.name)} ${club.position}/${club.slotCount}` : clubLabel(club.name)

/**
 * The customer's loyalty cards (handover 18a): one club at a time, the card
 * on the left and its details on the right. With more than one active club,
 * tabs across the top name each with its position ("Coffee Club 2/12"), as
 * the customer's own page does; a club they don't hold has no count. Arrow
 * keys move between tabs (the ARIA tabs pattern).
 */
export function LoyaltyPanel({
  customerId,
  customerName,
  clubs,
  onAdjust,
}: {
  customerId: string
  customerName: string
  clubs: CustomerClubDto[]
  onAdjust: (clubId: string) => void
}) {
  const [selected, setSelected] = useState(0)
  const id = useId()
  const tabs = useRef<(HTMLButtonElement | null)[]>([])
  const club = clubs[Math.min(selected, clubs.length - 1)]
  if (!club) return null

  const onKeyDown = (event: KeyboardEvent) => {
    const move = { ArrowRight: 1, ArrowLeft: -1 }[event.key]
    if (!move) return
    event.preventDefault()
    const to = (selected + move + clubs.length) % clubs.length
    setSelected(to)
    tabs.current[to]?.focus()
  }

  const shown = (
    // Keyed by club, so switching tabs starts each card's stack on its current card.
    <ClubCard key={club.clubId} customerId={customerId} customerName={customerName} club={club} others={clubs} onAdjust={() => onAdjust(club.clubId)} />
  )

  return (
    <Card className="flex flex-col gap-3 p-5">
      <h2 id={`${id}-h`} className="text-[1.02rem] font-bold">
        Loyalty cards
      </h2>
      {clubs.length > 1 ? (
        <>
          <div role="tablist" aria-labelledby={`${id}-h`} className="flex flex-wrap gap-2" onKeyDown={onKeyDown}>
            {clubs.map((c, i) => (
              <button
                key={c.clubId}
                ref={(el) => {
                  tabs.current[i] = el
                }}
                type="button"
                role="tab"
                id={`${id}-tab-${i}`}
                aria-selected={i === selected}
                aria-controls={`${id}-panel`}
                tabIndex={i === selected ? 0 : -1}
                className={`cursor-pointer rounded-full px-4 py-1.5 text-sm font-bold ${
                  i === selected ? 'bg-accent text-on-accent' : 'bg-accent-soft text-accent hover:bg-accent-soft/70'
                }`}
                onClick={() => setSelected(i)}
              >
                {tabLabel(c)}
              </button>
            ))}
          </div>
          <div role="tabpanel" id={`${id}-panel`} aria-labelledby={`${id}-tab-${selected}`}>
            {shown}
          </div>
        </>
      ) : (
        shown
      )}
    </Card>
  )
}
