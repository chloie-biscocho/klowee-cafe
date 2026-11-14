import { useState } from 'react'
import { useGiveCardMutation } from '../../../api/salesApi'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import { problemMessage } from '../../../lib/errors'
import { formatPeso } from '../../../lib/format'
import { availableRewards, clubSummary, memberClubs } from '../../../lib/loyalty'
import type { CustomerSummaryDto } from '../../../types/api'

/**
 * The line under the chosen customer: their tab and cards. Without a card,
 * "No loyalty card · Give card" (handover 16b): the owner hands one over on
 * the spot, after a confirm, so nothing is given silently. The card starts
 * today; an order dated earlier won't stamp it, and the line says so.
 */
export function LoyaltyLine({ customer, orderedOn }: { customer: CustomerSummaryDto; orderedOn: string }) {
  const [giveCard, { isLoading }] = useGiveCardMutation()
  const [confirming, setConfirming] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const members = memberClubs(customer.clubs)
  // The card to offer: the first active one they don't hold (The Coffee Club; the Matcha Club comes with it).
  const offer = customer.clubs.find((c) => c.membership !== 'member')
  const startsAfterOrder = members.filter((c) => c.joinedOn !== null && orderedOn < c.joinedOn)

  return (
    <div className="-mt-1 flex flex-col gap-1 text-xs text-muted">
      <p>
        Tab {formatPeso(customer.outstandingBalance)}
        {members.length === 0 && (
          <>
            {' · No loyalty card'}
            {offer && (
              <>
                {' · '}
                <button type="button" className="font-semibold text-accent hover:underline" onClick={() => setConfirming(true)}>
                  Give card
                </button>
              </>
            )}
          </>
        )}
        {members.map((club) => (
          <span key={club.clubId} className={availableRewards(club).length > 0 ? 'font-semibold text-success' : ''}>
            {' · '}
            {clubSummary(club)}
          </span>
        ))}
      </p>
      {startsAfterOrder.length > 0 && (
        <p className="text-warning">This order is dated before the card starts, so it won't stamp.</p>
      )}
      {error && (
        <p role="alert" className="text-danger">
          {error}
        </p>
      )}

      {offer && (
        <ConfirmDialog
          open={confirming}
          title={`Give ${customer.name} a card`}
          message={`Give ${customer.name} a ${offer.name.replace(/^the\s+/i, '')} card? This order will be their first stamp once it's paid.`}
          confirmLabel="Give card"
          loading={isLoading}
          onCancel={() => setConfirming(false)}
          onConfirm={async () => {
            setError(null)
            const result = await giveCard({ id: customer.id, body: { clubId: offer.clubId } })
            setConfirming(false)
            if ('error' in result) setError(problemMessage(result.error, "The card couldn't be given."))
          }}
        />
      )}
    </div>
  )
}
