import { formatPeso } from '../lib/format'
import { formatPaidOn } from './format'
import { CHANNEL_NAMES, type TabPayment } from './types'

/** The last few payments Klowee recorded on this tab. */
export function RecentPayments({ payments }: { payments: TabPayment[] }) {
  return (
    <section aria-labelledby="payments-heading" className="rounded-[22px] bg-panel px-4 pt-5 pb-[18px] sm:px-5 lg:px-7 lg:pt-7">
      <h2 id="payments-heading" className="mb-2.5 font-serif text-[1.25rem] text-deep">
        Recent payments
      </h2>
      {payments.length === 0 ? (
        <p className="text-soft">No payments yet.</p>
      ) : (
        <ul>
          {payments.map((payment, i) => (
            <li key={i} className={`flex justify-between gap-2.5 py-2 text-[0.96rem] ${i > 0 ? 'border-t border-dashed border-line' : ''}`}>
              <span>
                {CHANNEL_NAMES[payment.channel] === 'cash' ? 'Cash' : CHANNEL_NAMES[payment.channel]}
                <small className="block text-soft">{formatPaidOn(payment.paidOn)}</small>
              </span>
              <b className="whitespace-nowrap">{formatPeso(payment.amount)}</b>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
