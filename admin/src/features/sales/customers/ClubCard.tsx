import { useState } from 'react'
import { Link } from 'react-router'
import { useGiveCardMutation, useRedeemRewardMutation, useTakeCardMutation, useUnredeemRewardMutation } from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import { formatDate } from '../../../lib/format'
import { clubLabel } from '../../../lib/loyalty'
import type { CustomerClubDto, CustomerRewardDto } from '../../../types/api'
import { useToastedAction } from '../../toasts/useToastedAction'
import { StampCard } from './StampCard'

/** How far each card behind shows above and left of the one in front: a target big enough to click. */
const PEEK = 12

const STATE_STYLE: Record<CustomerRewardDto['state'], string> = {
  locked: 'text-muted',
  available: 'font-semibold text-success',
  used: 'text-muted line-through',
}

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
      className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-dashed border-line px-4 py-3 text-sm"
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

  return (
    <section aria-label={`${clubLabel(club.name)} card`} className="flex flex-col gap-3 rounded-lg border border-line p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="font-semibold">
          {club.name}
          <span className="ml-2 text-sm font-normal text-muted">
            {isCurrent
              ? `card ${club.cycle + 1}, ${club.position} of ${club.slotCount}`
              : `card ${shown?.cardNumber}, finished`}
            {club.joinedOn && ` · since ${formatDate(club.joinedOn)}`}
          </span>
        </h3>
        <div className="flex gap-1.5">
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
            className="absolute rounded-lg border border-line bg-canvas motion-safe:transition-transform motion-safe:hover:-translate-y-0.5"
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
            className="relative z-10 rounded-lg border border-line bg-surface p-3 shadow-sm motion-safe:animate-[card-front_150ms_ease-out]"
          >
            <StampCard card={shown} />
          </div>
        )}
      </div>
      {isCurrent && earlierReady.length > 0 && (
        <p className="text-sm">
          Still to use from{' '}
          {earlierReady.map((c, i) => (
            <span key={c.cardNumber}>
              {i > 0 && ', '}
              <button type="button" className="text-accent hover:underline" onClick={() => setShownNumber(c.cardNumber)}>
                card {c.cardNumber}
              </button>
            </span>
          ))}
          .
        </p>
      )}

      {isCurrent && club.pendingDrinks > 0 && (
        <p className="text-sm text-muted">
          {club.pendingDrinks} drink{club.pendingDrinks === 1 ? '' : 's'} on the tab will stamp once paid.
        </p>
      )}

      <ul className="flex flex-col gap-1.5 text-sm">
        {(shown?.rewards ?? []).map((reward) => (
          <li key={`${reward.rewardId}-${reward.cycle}`} className="flex flex-wrap items-center justify-between gap-2">
            <span>
              <span className={STATE_STYLE[reward.state]}>{reward.label}</span>
              <span className="text-muted">
                {' '}
                · slot {reward.slot} · {reward.state === 'available' ? 'ready to use' : reward.state}
              </span>
              {reward.orderId && (
                <Link className="ml-2 text-accent hover:underline" to={`/sales/orders/${reward.orderId}`}>
                  on this order
                </Link>
              )}
            </span>
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
