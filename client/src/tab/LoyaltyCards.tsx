import { useId, useRef, useState, type KeyboardEvent } from 'react'
import { PillButton } from '../components/Button'
import { clubLabel, formatLongDate, formatShortDate, stampDate, stampTilt } from './format'
import type { TabCard, TabClub } from './types'
import type { WriteResult } from './useTab'

/** The stamp printed in a plain slot: a bean heart for coffee, a leaf for matcha. */
function SlotIcon({ style }: { style: TabClub['style'] }) {
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

/** The three hand-drawn squiggles in the printed card's corners. */
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

/** A rewards' words on two lines, as printed: "50%" / "OFF". */
const printed = (label: string) => label.toUpperCase().replace(' ', '\n')

/** How a reward reads in a sentence: "50% OFF" → "50% off". */
const phrase = (label: string) => label.toLowerCase()

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? '' : 's'}`

/**
 * One card face, drawn from the data like the printed card: the name in the
 * heavy display type, the slots in two rows, rewards in pink and the last in
 * the club's colour. A filled slot gets a see-through rubber stamp (SIPPED, or
 * CLAIMED on a used reward) tilted like the admin's. Dimmed with every slot
 * empty when the customer has no card yet.
 */
function CardFace({ club, card, live, next }: { club: TabClub; card: TabCard | null; live: boolean; next: number | null }) {
  const rewards = card?.rewards ?? club.rewards
  const rewardOn = new Map(rewards.map((r) => [r.slot, r]))
  const lastRewardSlot = Math.max(0, ...rewards.map((r) => r.slot))
  const slots = card?.slots ?? Array.from({ length: club.slotCount }, (_, i) => ({ slot: i + 1, filled: false, date: null, source: null }))
  const matcha = club.style === 'Matcha'
  const clubText = matcha ? 'text-matcha' : 'text-cobalt'
  const clubBg = matcha ? 'bg-matcha' : 'bg-cobalt'
  const clubBgDim = matcha ? 'bg-matcha/30' : 'bg-cobalt/30'
  const clubRing = matcha ? 'shadow-[inset_0_0_0_2.5px_var(--color-matcha)]' : 'shadow-[inset_0_0_0_2.5px_var(--color-cobalt)]'

  return (
    // Dimmed (no card yet): the slots, stamps and squiggles fade, the lettering
    // keeps full contrast so it stays readable.
    <div className="relative overflow-hidden rounded-[18px] bg-card px-3.5 pt-[18px] pb-4 text-center text-card-ink">
      <div className={live ? '' : 'opacity-40'}>
        <Squiggles />
      </div>
      <p aria-hidden="true" className="px-5 font-hand text-[0.58rem] font-medium tracking-[.1em] sm:text-[0.66rem] sm:tracking-[.14em]">
        KLOWEE CAFE'S LOYALTY CARD PRESENTS
      </p>
      <p aria-hidden="true" className={`mt-0.5 mb-3 font-display text-[clamp(1.55rem,7.4vw,2.05rem)] leading-[1.05] tracking-[.01em] lg:text-[1.9rem] ${clubText}`}>
        {club.name.toUpperCase()}
      </p>
      <ol
        aria-hidden="true"
        className="mx-auto grid max-w-[360px] gap-x-1.5 gap-y-1"
        style={{ gridTemplateColumns: `repeat(${Math.ceil(club.slotCount / 2)}, minmax(0, 1fr))` }}
      >
        {slots.map((slot) => {
          const reward = rewardOn.get(slot.slot)
          const isLast = reward?.slot === lastRewardSlot
          const used = reward?.state === 'used'
          const ready = live && reward?.state === 'available'
          return (
            <li key={slot.slot} className="flex flex-col items-center">
              <span className="h-4 font-hand text-[0.62rem] leading-4 font-bold text-card-ink tabular-nums">
                {live && slot.filled && slot.date ? stampDate(slot.date) : ''}
              </span>
              <span
                className={`relative grid aspect-square w-full place-content-center rounded-full ${
                  reward ? (!live ? (isLast ? clubBgDim : 'bg-card-pink/40') : isLast ? clubBg : 'bg-card-pink') : 'bg-card-slot'
                } ${ready ? 'shadow-[0_0_0_3px_var(--color-yellow)]' : next === slot.slot ? clubRing : ''}`}
              >
                {reward ? (
                  <span
                    className={`px-0.5 font-hand text-[clamp(0.5rem,2.5vw,0.66rem)] leading-[1.1] font-bold tracking-[.04em] whitespace-pre-line ${
                      // Dark ink on the pink (white measured 2.7:1); cream on the club colour.
                      isLast && live ? 'text-card' : 'text-card-ink'
                    }`}
                  >
                    {printed(reward.label)}
                  </span>
                ) : (
                  <span className={live ? '' : 'opacity-35'}>
                    <SlotIcon style={club.style} />
                  </span>
                )}
                {live && slot.filled && (
                  // The rubber stamp is decoration over the slot's own design; its
                  // word is CSS content, not text, and the card's description
                  // says the same in words for screen readers.
                  <span
                    data-stamp={used ? 'CLAIMED' : 'SIPPED'}
                    className={`absolute top-1/2 left-1/2 rounded-[3px] border-[1.5px] px-[2px] font-display text-[clamp(0.34rem,1.55vw,0.5rem)] leading-[1.3] opacity-80 before:content-[attr(data-stamp)] ${
                      isLast ? 'border-card text-card' : 'border-cobalt text-cobalt'
                    }`}
                    style={{ transform: `translate(-50%, -50%) rotate(${stampTilt(slot.slot)}deg)` }}
                  />
                )}
              </span>
            </li>
          )
        })}
      </ol>
      <p aria-hidden="true" className="mt-3 text-[0.72rem] font-bold tracking-[.08em]">
        COLLECT THE STAMP AND GET A FREEBIE!
      </p>
    </div>
  )
}

/** What a screen reader hears for a card: every slot, in words. */
function cardDescription(club: TabClub, card: TabCard): string {
  const rewardOn = new Map(card.rewards.map((r) => [r.slot, r]))
  const filled = card.slots.filter((s) => s.filled)
  const parts = card.slots
    .filter((s) => s.filled || rewardOn.has(s.slot))
    .map((s) => {
      const reward = rewardOn.get(s.slot)
      const when = s.filled ? (s.date ? `stamped ${formatLongDate(s.date)}` : 'stamped') : 'not stamped yet'
      return `slot ${s.slot}${reward ? ` (${phrase(reward.label)}, ${reward.state})` : ''}: ${when}`
    })
  return `${clubLabel(club.name)}, card ${card.cardNumber}: ${filled.length} of ${club.slotCount} stamped. ${parts.join('; ')}.`
}

/** The sentence under a member's card, built from the data. */
function cardSentence(club: TabClub, card: TabCard, cards: TabCard[]): string[] {
  if (!card.isCurrent) {
    const left = card.rewards.filter((r) => r.state === 'available')
    return [`Card ${card.cardNumber}, finished.`, ...left.map((r) => `${capitalize(phrase(r.label))} still to use.`)]
  }
  const lines: string[] = []
  const used = card.rewards.filter((r) => r.state === 'used')
  const ready = card.rewards.filter((r) => r.state === 'available')
  const upcoming = card.rewards.filter((r) => r.state === 'locked').sort((a, b) => a.slot - b.slot)[0]
  let first = `${club.position} of ${club.slotCount}.`
  if (used.length > 0) first += ` ${capitalize(used.map((r) => phrase(r.label)).join(' and '))} used.`
  lines.push(first)
  for (const r of ready) lines.push(`${capitalize(phrase(r.label))} is ready to use on your next order.`)
  if (upcoming) lines.push(`Next up: ${phrase(upcoming.label)} in ${plural(upcoming.slot - club.position, 'more drink')}.`)
  const leftovers = cards.filter((c) => !c.isCurrent).flatMap((c) => c.rewards.filter((r) => r.state === 'available').map((r) => ({ r, c })))
  for (const { r, c } of leftovers) lines.push(`Still to use from card ${c.cardNumber}: ${phrase(r.label)}.`)
  if (club.pendingDrinks > 0) {
    lines.push(`${plural(club.pendingDrinks, 'drink')} on your tab will stamp your card once paid.`)
  }
  return lines
}

const capitalize = (s: string) => s.charAt(0).toUpperCase() + s.slice(1)

/** A member's cards: the current one in front, finished ones behind, "Card 2 of 3". */
function CardStack({ club }: { club: TabClub }) {
  const cards = [...club.cards].sort((a, b) => a.cardNumber - b.cardNumber)
  const currentIndex = Math.max(0, cards.findIndex((c) => c.isCurrent))
  const [index, setIndex] = useState(currentIndex)
  const touchStart = useRef<number | null>(null)
  const card = cards[Math.min(index, cards.length - 1)]
  if (!card) return null
  const behind = Math.min(2, cards.length - 1)
  const next = card.isCurrent && club.position < club.slotCount ? club.position + 1 : null
  const go = (to: number) => setIndex(Math.max(0, Math.min(cards.length - 1, to)))

  return (
    <div>
      <div
        role="group"
        aria-roledescription="card"
        aria-label={cardDescription(club, card)}
        className="relative touch-pan-y"
        style={{ marginBottom: behind * 8 }}
        onTouchStart={(event) => (touchStart.current = event.touches[0]?.clientX ?? null)}
        onTouchEnd={(event) => {
          const start = touchStart.current
          const end = event.changedTouches[0]?.clientX
          touchStart.current = null
          if (start === null || end === undefined || Math.abs(end - start) < 40) return
          go(index + (end < start ? 1 : -1))
        }}
      >
        {/* The cards behind peek out below the front one. */}
        {Array.from({ length: behind }, (_, i) => (
          <div
            key={i}
            aria-hidden="true"
            className="absolute inset-x-0 top-0 h-full rounded-[18px] bg-card shadow-[0_1px_0_var(--color-line)]"
            style={{ transform: `translateY(${(i + 1) * 8}px) scale(${1 - (i + 1) * 0.03})`, opacity: 0.9 - i * 0.25, zIndex: -1 - i }}
          />
        ))}
        <div className="relative">
          <CardFace club={club} card={card} live next={next} />
        </div>
      </div>
      {cards.length > 1 && (
        <div className="mt-2 flex items-center justify-center gap-3">
          <button
            type="button"
            className="cursor-pointer rounded-full bg-sky px-3 py-1.5 font-bold text-deep disabled:cursor-default disabled:opacity-40"
            aria-label="Previous card"
            disabled={index === 0}
            onClick={() => go(index - 1)}
          >
            ←
          </button>
          <span aria-live="polite" className="min-w-24 text-center text-[0.95rem] font-bold text-deep">
            Card {card.cardNumber} of {cards.length}
          </span>
          <button
            type="button"
            className="cursor-pointer rounded-full bg-sky px-3 py-1.5 font-bold text-deep disabled:cursor-default disabled:opacity-40"
            aria-label="Next card"
            disabled={index === cards.length - 1}
            onClick={() => go(index + 1)}
          >
            →
          </button>
        </div>
      )}
      <p className="mt-3 text-[0.95rem] text-soft">
        {cardSentence(club, card, cards).map((line, i) => (
          <span key={i}>
            {i === 0 ? <b className="text-deep">{line}</b> : line}{' '}
          </span>
        ))}
      </p>
    </div>
  )
}

/** A club the customer has no card in: dimmed, with the way to get one. */
function NoCard({ club, clubs, onRequest }: { club: TabClub; clubs: TabClub[]; onRequest: (clubId: string) => Promise<WriteResult> }) {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const source = club.comesWithClubId ? clubs.find((c) => c.clubId === club.comesWithClubId) : undefined
  // The card to ask for: this one, or the one it comes with.
  const askFor = source ?? club
  const asked = askFor.membership === 'requested'

  return (
    <div>
      <div role="img" aria-label={`${clubLabel(club.name)} card, not yet yours. Its rewards: ${club.rewards.map((r) => `${phrase(r.label)} on slot ${r.slot}`).join(', ')}.`}>
        <CardFace club={club} card={null} live={false} next={null} />
      </div>
      <div className="mt-3 text-[0.95rem] text-soft">
        {source ? (
          <p>Comes with your {clubLabel(source.name)} card.</p>
        ) : (
          !asked && <p>You don't have a {clubLabel(club.name)} card yet.</p>
        )}
        {asked && askFor.requestedOn ? (
          <p className="mt-1 font-bold text-deep">
            Asked on {formatShortDate(askFor.requestedOn)}. Klowee will set it up.
          </p>
        ) : (
          askFor.membership === 'none' && (
            <PillButton
              className="mt-3"
              disabled={busy}
              onClick={async () => {
                setBusy(true)
                setError(null)
                const result = await onRequest(askFor.clubId)
                if (!result.ok) setError(result.message)
                setBusy(false)
              }}
            >
              {source ? `Ask for a ${clubLabel(source.name)} card` : 'Ask for a card'}
            </PillButton>
          )
        )}
        {error && (
          <p role="alert" className="mt-2 font-bold text-deep">
            {error}
          </p>
        )}
      </div>
    </div>
  )
}

function ClubPanel({ club, clubs, onRequest }: { club: TabClub; clubs: TabClub[]; onRequest: (clubId: string) => Promise<WriteResult> }) {
  return club.membership === 'member' && club.cards.length > 0 ? (
    <CardStack club={club} />
  ) : (
    <NoCard club={club} clubs={clubs} onRequest={onRequest} />
  )
}

const tabLabel = (club: TabClub) =>
  club.membership === 'member' ? `${clubLabel(club.name)} ${club.position}/${club.slotCount}` : clubLabel(club.name)

/**
 * "Your loyalty cards": one tab per active club, one card visible; no tabs
 * with a single club. Arrow keys move between tabs (the ARIA tabs pattern).
 */
export function LoyaltyCards({ clubs, onRequest }: { clubs: TabClub[]; onRequest: (clubId: string) => Promise<WriteResult> }) {
  const [selected, setSelected] = useState(0)
  const id = useId()
  const tabs = useRef<(HTMLButtonElement | null)[]>([])
  if (clubs.length === 0) return null
  const club = clubs[Math.min(selected, clubs.length - 1)]!

  const onKeyDown = (event: KeyboardEvent) => {
    const move = { ArrowRight: 1, ArrowLeft: -1 }[event.key]
    if (!move) return
    event.preventDefault()
    const to = (selected + move + clubs.length) % clubs.length
    setSelected(to)
    tabs.current[to]?.focus()
  }

  return (
    <section aria-labelledby={`${id}-h`} className="rounded-[22px] bg-panel px-5 pt-5 pb-[18px]">
      <h2 id={`${id}-h`} className="mb-2.5 font-serif text-[1.25rem] text-deep">
        Your loyalty cards
      </h2>
      {clubs.length > 1 ? (
        <>
          <div role="tablist" aria-label="Loyalty cards" className="mb-3 flex gap-2" onKeyDown={onKeyDown}>
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
                className={`flex-1 cursor-pointer rounded-full border-0 px-2.5 py-2 font-bold ${
                  i === selected ? 'bg-deep text-sky' : 'bg-sky text-deep'
                }`}
                onClick={() => setSelected(i)}
              >
                {tabLabel(c)}
              </button>
            ))}
          </div>
          <div role="tabpanel" id={`${id}-panel`} aria-labelledby={`${id}-tab-${selected}`}>
            <ClubPanel key={club.clubId} club={club} clubs={clubs} onRequest={onRequest} />
          </div>
        </>
      ) : (
        <ClubPanel club={club} clubs={clubs} onRequest={onRequest} />
      )}
    </section>
  )
}

