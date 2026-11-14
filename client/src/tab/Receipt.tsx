import { useState } from 'react'
import { PillButton } from '../components/Button'
import { formatMenuPrice, formatPeso } from '../lib/format'
import { formatShortDate } from './format'
import { PayDialog } from './PayDialog'
import { CHANNEL_NAMES, type ClaimRequest, type PublicTab } from './types'
import type { WriteResult } from './useTab'

/**
 * "your tab": the menu's cream paper sheet in hand-printed type, one row per
 * unpaid order with a dotted leader, a ruled total. Below it, what's been
 * reported and is waiting for Klowee, a rejected report's reason, and Pay.
 */
export function Receipt({ tab, onClaim }: { tab: PublicTab; onClaim: (claim: ClaimRequest) => Promise<WriteResult> }) {
  const [paying, setPaying] = useState(false)
  const pending = tab.claims.filter((c) => c.status === 'Pending')
  const latest = tab.claims[0]
  const rejected = latest?.status === 'Rejected' ? latest : null
  const remaining = Math.max(0, Math.round((tab.balance - tab.pendingClaimsTotal) * 100) / 100)
  const methods = tab.pay.methods

  return (
    <section aria-labelledby="receipt-heading" className="rounded-md bg-paper px-5 pt-[22px] pb-[18px] font-hand text-menu-ink">
      <h1 id="receipt-heading" className="text-center text-[1.05rem] font-bold tracking-[.06em]">
        your tab
      </h1>
      <p className="mb-3.5 text-center text-[0.9rem] text-menu-soft">{tab.name} · as of today</p>

      {tab.balance <= 0 ? (
        <p className="border-t-2 border-menu-ink pt-2.5 text-center text-[1.25rem] font-bold">you're all paid up</p>
      ) : (
        <>
          <ul>
            {tab.unpaidOrders.map((order, i) => (
              <li key={i} className="flex items-baseline gap-2 py-[3px]">
                <span className="lowercase">
                  {order.items}
                  <small className="block text-[0.82rem] text-menu-soft">
                    {formatShortDate(order.orderedOn).toLowerCase()}
                    {order.paid > 0 && ` · ${formatMenuPrice(order.paid)} paid`}
                  </small>
                </span>
                <span aria-hidden="true" className="min-w-3.5 flex-1 -translate-y-1 border-b-2 border-dotted border-menu-rule" />
                <span className="tabular-nums">{formatMenuPrice(order.due)}</span>
              </li>
            ))}
          </ul>
          <p className="mt-2.5 flex justify-between border-t-2 border-menu-ink pt-2.5 text-[1.25rem] font-bold">
            <span>you owe</span>
            <span>{formatPeso(tab.balance)}</span>
          </p>
        </>
      )}

      <div className="font-sans">
        {pending.map((claim) => (
          <p key={claim.id} className="mt-3 rounded-xl bg-yellow px-3 py-2.5 text-[0.93rem] text-sticker-ink">
            {formatPeso(claim.amount)} reported as paid by {CHANNEL_NAMES[claim.channel]}. Waiting for Klowee to confirm.
          </p>
        ))}
        {rejected && (
          <p className="mt-3 rounded-xl border-2 border-pink bg-panel px-3 py-2.5 text-[0.93rem] text-ink">
            Klowee couldn't confirm your {formatPeso(rejected.amount)} by {CHANNEL_NAMES[rejected.channel]} (ref {rejected.reference}):{' '}
            {rejected.rejectReason}
          </p>
        )}
        {remaining > 0 &&
          (methods.length > 0 ? (
            <PillButton className="mt-3.5 block w-full" onClick={() => setPaying(true)}>
              {methods.length === 1 ? `Pay with ${methods[0]!.label}` : 'Pay'}
            </PillButton>
          ) : (
            <p className="mt-3.5 text-[0.95rem] text-menu-soft">To pay, see Klowee in person: cash or a transfer at the cart.</p>
          ))}
      </div>

      {methods.length > 0 && (
        <PayDialog
          open={paying}
          onClose={() => setPaying(false)}
          methods={methods}
          note={tab.pay.note}
          amountDue={remaining}
          onSubmit={onClaim}
        />
      )}
    </section>
  )
}
