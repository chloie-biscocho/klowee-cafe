import { Link } from 'react-router'
import { useRedeemRewardMutation, useUnredeemRewardMutation } from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { clubLabel } from '../../../lib/loyalty'
import type { CustomerClubDto, CustomerRewardDto } from '../../../types/api'
import { useToastedAction } from '../../toasts/useToastedAction'

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
export function ClubCard({ customerId, club, onAdjust }: { customerId: string; club: CustomerClubDto; onAdjust: () => void }) {
  const [redeem, { isLoading: isRedeeming }] = useRedeemRewardMutation()
  const [unredeem, { isLoading: isUnredeeming }] = useUnredeemRewardMutation()
  const run = useToastedAction()

  const current = club.rewards.filter((r) => r.cycle === club.cycle)
  const older = club.rewards.filter((r) => r.cycle < club.cycle)
  const onSlot = new Map(current.map((r) => [r.slot, r]))
  const slots = Array.from({ length: club.slotCount }, (_, i) => i + 1)

  const markUsed = (reward: CustomerRewardDto) =>
    run(
      redeem({ id: customerId, body: { clubId: club.clubId, rewardId: reward.rewardId, cycle: reward.cycle, note: 'Marked used in the admin' } }),
      `${reward.label} marked used.`,
    )
  const makeAvailable = (reward: CustomerRewardDto) =>
    run(unredeem({ id: customerId, redemptionId: reward.redemptionId! }), `${reward.label} is available again.`)

  return (
    <section aria-label={`${clubLabel(club.name)} card`} className="flex flex-col gap-3 rounded-lg border border-line p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="font-semibold">
          {club.name}
          <span className="ml-2 text-sm font-normal text-muted">
            {club.position} of {club.slotCount}
            {club.cycle > 0 && ` · card ${club.cycle + 1}`}
          </span>
        </h3>
        <Button size="sm" onClick={onAdjust}>
          Adjust stamps
        </Button>
      </div>

      <ol aria-label={`${club.position} of ${club.slotCount} slots filled`} className="flex flex-wrap gap-1.5">
        {slots.map((slot) => {
          const filled = slot <= club.position
          const next = slot === club.position + 1
          const reward = onSlot.get(slot)
          return (
            <li key={slot} className="flex w-14 flex-col items-center gap-0.5">
              <span
                aria-label={`Slot ${slot}${filled ? ', filled' : next ? ', next' : ''}${reward ? `, ${reward.label} ${reward.state}` : ''}`}
                className={`flex size-9 items-center justify-center rounded-full border text-xs tabular-nums ${
                  filled
                    ? 'border-accent bg-accent text-white'
                    : next
                      ? 'border-accent ring-2 ring-accent/30'
                      : reward
                        ? 'border-dashed border-line'
                        : 'border-line'
                }`}
              >
                {slot}
              </span>
              {reward && <span className={`text-center text-[10px] leading-tight ${STATE_STYLE[reward.state]}`}>{reward.label}</span>}
            </li>
          )
        })}
      </ol>

      {club.pendingDrinks > 0 && (
        <p className="text-sm text-muted">
          {club.pendingDrinks} drink{club.pendingDrinks === 1 ? '' : 's'} on the tab will stamp once paid.
        </p>
      )}

      <ul className="flex flex-col gap-1.5 text-sm">
        {[...older, ...current].map((reward) => (
          <li key={`${reward.rewardId}-${reward.cycle}`} className="flex flex-wrap items-center justify-between gap-2">
            <span>
              <span className={STATE_STYLE[reward.state]}>{reward.label}</span>
              <span className="text-muted">
                {' '}
                · slot {reward.slot}
                {reward.cycle < club.cycle && `, from card ${reward.cycle + 1}`} · {reward.state === 'available' ? 'ready to use' : reward.state}
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
    </section>
  )
}
