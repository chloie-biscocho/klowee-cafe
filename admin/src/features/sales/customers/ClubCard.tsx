import { useState } from 'react'
import { Link } from 'react-router'
import { useListClubsQuery } from '../../../api/loyaltyApi'
import { useGiveCardMutation, useRedeemRewardMutation, useTakeCardMutation, useUnredeemRewardMutation } from '../../../api/salesApi'
import { Badge } from '../../../components/ui/Badge'
import { Button } from '../../../components/ui/Button'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import { formatDate } from '../../../lib/format'
import { clubLabel, rewardPhrase } from '../../../lib/loyalty'
import type { CustomerClubDto, CustomerRewardDto } from '../../../types/api'
import { useToastedAction } from '../../toasts/useToastedAction'
import { RecentStamps } from './RecentStamps'
import { StampCard } from './StampCard'

/** How far each card behind shows above and left of the one in front: a target big enough to click. */
const PEEK = 12

const STATE_STYLE: Record<CustomerRewardDto['state'], string> = {
  locked: 'font-medium',
  available: 'font-bold',
  used: 'text-muted line-through',
}

const STATE_LABEL: Record<CustomerRewardDto['state'], string> = { locked: 'locked', available: 'ready', used: 'used' }

/**
 * One loyalty card as the customer would hold it (decision 020): a row of
 * slots, filled up to their place on the current card, the next one ringed,
 * and the rewards printed on their slots. Rewards can be marked used by hand
 * (from the paper card) or made available again, unless an order used them.
 */
export function ClubCard({
  customerId,
  customerName,
  club,
  others = [],
  onAdjust,
}: {
  customerId: string
  customerName: string
  club: CustomerClubDto
  /** The customer's other cards, named in the take-away warning (they stay). */
  others?: CustomerClubDto[]
  onAdjust: () => void
}) {
  if (club.membership !== 'member') return <NoCard customerId={customerId} club={club} />
  return <MemberCard customerId={customerId} customerName={customerName} club={club} others={others} onAdjust={onAdjust} />
}

/** Cards are by invitation (handover 16a): a quiet row and the one action. */
function NoCard({ customerId, club }: { customerId: string; club: CustomerClubDto }) {
  const [giveCard, { isLoading }] = useGiveCardMutation()
  const run = useToastedAction()
  return (
    <section
      aria-label={`${clubLabel(club.name)} card`}
      className="flex flex-wrap items-center justify-between gap-2 rounded-2xl border-2 border-dashed border-line px-4 py-3 text-sm"
    >
      <span className="text-muted">
        No {clubLabel(club.name)} card{club.membership === 'requested' && ' (asked for one)'}. Drinks earn no stamps.
      </span>
      <Button size="sm" loading={isLoading} onClick={() => void run(giveCard({ id: customerId, body: { clubId: club.clubId } }), `${club.name} card given.`)}>
        Give card
      </Button>
    </section>
  )
}

function MemberCard({
  customerId,
  customerName,
  club,
  others,
  onAdjust,
}: {
  customerId: string
  customerName: string
  club: CustomerClubDto
  others: CustomerClubDto[]
  onAdjust: () => void
}) {
  // The pink letter in the club's name comes from the clubs list (handover 17a),
  // which the admin already loads; the customer payload doesn't carry it.
  const { data: clubs } = useListClubsQuery()
  const accentLetter = clubs?.find((c) => c.id === club.clubId)?.accentLetter ?? null
  const [takeCard, { isLoading: isTaking }] = useTakeCardMutation()
  const [confirming, setConfirming] = useState(false)
  const [redeem, { isLoading: isRedeeming }] = useRedeemRewardMutation()
  const [unredeem, { isLoading: isUnredeeming }] = useUnredeemRewardMutation()
  const run = useToastedAction()

  // The stack (handover 16b): the current card first; finished ones behind it.
  const cards = club.cards
  const currentNumber = cards.find((c) => c.isCurrent)?.cardNumber ?? cards.length
  const [shownNumber, setShownNumber] = useState(currentNumber)
  const shown = cards.find((c) => c.cardNumber === shownNumber) ?? cards[cards.length - 1]
  const isCurrent = shown?.isCurrent ?? true
  // Behind the shown card: the others, newest nearest the top, edges showing (three at most).
  const behind = cards.filter((c) => c !== shown).sort((a, b) => b.cardNumber - a.cardNumber).slice(0, 3)
  // Rewards still to use on earlier cards, so they aren't missed while the current card shows.
  const earlierReady = cards.filter((c) => !c.isCurrent && c.rewards.some((r) => r.state === 'available'))

  const markUsed = (reward: CustomerRewardDto) =>
    run(
      redeem({ id: customerId, body: { clubId: club.clubId, rewardId: reward.rewardId, cycle: reward.cycle, note: 'Marked used in the admin' } }),
      `${reward.label} marked used.`,
    )
  const makeAvailable = (reward: CustomerRewardDto) =>
    run(unredeem({ id: customerId, redemptionId: reward.redemptionId! }), `${reward.label} is available again.`)

  // Anything on the card goes with it; the API wants that said out loud (force).
  const stamps = club.cycle * club.slotCount + club.position
  const used = club.rewards.filter((r) => r.state === 'used').length
  const otherCards = others.filter((c) => c.clubId !== club.clubId && c.membership === 'member')
  const takeAwayMessage = [
    `Take ${customerName}'s ${club.name} card away? Their drinks stop earning stamps on it.`,
    stamps > 0 || used > 0
      ? `Its ${stamps} stamp${stamps === 1 ? '' : 's'}${used > 0 ? ` and ${used} used reward${used === 1 ? '' : 's'}` : ''} are voided.`
      : '',
    otherCards.length > 0 ? `Their ${otherCards.map((c) => c.name).join(' and ')} card stays.` : '',
  ]
    .filter(Boolean)
    .join(' ')

  // Where they are, in one sentence from the data: what's ready, else what's next.
  const currentCard = cards.find((c) => c.isCurrent)
  const ready = (currentCard?.rewards ?? []).filter((r) => r.state === 'available')
  const upcoming = (currentCard?.rewards ?? []).filter((r) => r.state === 'locked').sort((a, b) => a.slot - b.slot)[0]
  const nextLine =
    ready.length > 0
      ? `${ready.map((r) => rewardPhrase(r.label)).join(' and ').replace(/^./, (c) => c.toUpperCase())} ${ready.length === 1 ? 'is' : 'are'} ready to use.`
      : upcoming
        ? `Next: ${rewardPhrase(upcoming.label)} in ${upcoming.slot - club.position} more drink${upcoming.slot - club.position === 1 ? '' : 's'}.`
        : null

  // Phone: one column (CSS order): where they are, card, rewards, recent stamps,
  // actions. From 900px the two wrappers become columns: the card on the left,
  // everything else on the right.
  const column = 'contents wide:flex wide:min-w-0 wide:flex-col wide:gap-4'

  return (
    <section aria-label={`${clubLabel(club.name)} card`} className="flex flex-col gap-3 rounded-2xl border-[1.5px] border-line p-4">
      <h3 className="font-bold">{club.name}</h3>

      <div className="flex flex-col gap-4 wide:grid wide:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] wide:items-start wide:gap-6">
        <div className={column}>
          <div className="order-2 flex flex-col gap-3 wide:order-none">
            {cards.length > 1 && (
              <div className="flex items-center gap-2 text-sm" aria-label="Cards">
                <Button
                  size="sm"
                  variant="ghost"
                  aria-label="Previous card"
                  disabled={shownNumber <= 1}
                  onClick={() => setShownNumber((n) => Math.max(1, n - 1))}
                >
                  ‹
                </Button>
                <span aria-live="polite">
                  Card {shown?.cardNumber} of {cards.length}
                </span>
                <Button
                  size="sm"
                  variant="ghost"
                  aria-label="Next card"
                  disabled={shownNumber >= cards.length}
                  onClick={() => setShownNumber((n) => Math.min(cards.length, n + 1))}
                >
                  ›
                </Button>
              </div>
            )}

            {/* Each card behind peeks out PEEK px above and left of the one in front: a clickable edge. */}
            <div className="relative" style={{ paddingTop: behind.length * PEEK, paddingLeft: behind.length * PEEK }}>
              {behind.map((card, index) => (
                <button
                  key={card.cardNumber}
                  type="button"
                  aria-label={`Show card ${card.cardNumber}`}
                  onClick={() => setShownNumber(card.cardNumber)}
                  className="absolute rounded-[18px] border-[1.5px] border-line bg-card motion-safe:transition-transform motion-safe:hover:-translate-y-0.5"
                  style={{
                    // The newest card behind sits nearest the front.
                    top: (behind.length - 1 - index) * PEEK,
                    left: (behind.length - 1 - index) * PEEK,
                    right: (index + 1) * PEEK,
                    bottom: (index + 1) * PEEK,
                    zIndex: behind.length - index,
                  }}
                />
              ))}
              {shown && (
                <div
                  key={shown.cardNumber}
                  className="relative z-10 rounded-[18px] shadow-sm motion-safe:animate-[card-front_150ms_ease-out]"
                >
                  <StampCard
                    card={shown}
                    clubName={club.name}
                    style={club.style}
                    accentLetter={accentLetter}
                    next={isCurrent && club.position < club.slotCount ? club.position + 1 : null}
                  />
                </div>
              )}
            </div>
          </div>
        </div>

        <div className={column}>
          {/* Where they are. */}
          <div className="order-1 wide:order-none">
            <p className="font-serif text-[1.6rem] leading-tight text-accent">
              Card {club.cycle + 1} · {club.position} of {club.slotCount}
            </p>
            {nextLine && <p className="mt-1 text-sm">{nextLine}</p>}
            {club.pendingDrinks > 0 && (
              <p className="mt-1 text-sm text-muted">
                {club.pendingDrinks} drink{club.pendingDrinks === 1 ? '' : 's'} on the tab will stamp once paid.
              </p>
            )}
            {club.joinedOn && <p className="mt-1 text-xs text-muted">Card holder since {formatDate(club.joinedOn)}</p>}
          </div>

          {/* Rewards on the card being shown. */}
          <div className="order-3 wide:order-none">
            <h4 className="mb-1.5 text-[0.8rem] font-bold text-muted">
              Rewards on card {shown?.cardNumber}
              {!isCurrent && ' (finished)'}
            </h4>
            <ul className="flex flex-col divide-y-[1.5px] divide-line rounded-2xl border-[1.5px] border-line text-sm">
              {isCurrent && earlierReady.length > 0 && (
                <li className="px-3.5 py-2.5">
                  Still to use from{' '}
                  {earlierReady.map((c, i) => (
                    <span key={c.cardNumber}>
                      {i > 0 && ', '}
                      <button type="button" className="cursor-pointer font-bold text-accent hover:underline" onClick={() => setShownNumber(c.cardNumber)}>
                        card {c.cardNumber}
                      </button>
                    </span>
                  ))}
                  .
                </li>
              )}
              {(shown?.rewards ?? []).map((reward) => (
                <li key={`${reward.rewardId}-${reward.cycle}`} className="flex flex-wrap items-center justify-between gap-2 px-3.5 py-2">
                  <span className="flex flex-wrap items-center gap-2">
                    <span className={STATE_STYLE[reward.state]}>{reward.label}</span>
                    <span className="text-muted">slot {reward.slot}</span>
                    <Badge tone={reward.state === 'available' ? 'success' : 'neutral'}>{STATE_LABEL[reward.state]}</Badge>
                  </span>
                  {reward.orderId && (
                    <Link className="font-bold text-accent hover:underline" to={`/sales/orders/${reward.orderId}`}>
                      on this order
                    </Link>
                  )}
                  {reward.state === 'available' && (
                    <Button size="sm" variant="ghost" loading={isRedeeming} onClick={() => void markUsed(reward)}>
                      Mark used
                    </Button>
                  )}
                  {reward.state === 'used' && reward.redemptionId && !reward.orderId && (
                    <Button size="sm" variant="ghost" loading={isUnredeeming} onClick={() => void makeAvailable(reward)}>
                      Make available
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          </div>

          <RecentStamps customerId={customerId} club={club} />

          <div className="order-5 flex flex-wrap gap-2 wide:order-none">
            {isCurrent && (
              <Button size="sm" onClick={onAdjust}>
                Adjust stamps
              </Button>
            )}
            <Button size="sm" variant="ghost" onClick={() => setConfirming(true)}>
              Take card away
            </Button>
          </div>
        </div>
      </div>

      <ConfirmDialog
        open={confirming}
        title={`Take the ${club.name} card away`}
        message={takeAwayMessage}
        confirmLabel="Take card away"
        loading={isTaking}
        onCancel={() => setConfirming(false)}
        onConfirm={async () => {
          const taken = await run(
            takeCard({ id: customerId, membershipId: club.membershipId!, force: stamps > 0 || used > 0 }),
            `${club.name} card taken away.`,
          )
          if (taken.ok) setConfirming(false)
        }}
      />
    </section>
  )
}
