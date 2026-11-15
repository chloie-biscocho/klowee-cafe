import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { useListCustomersQuery, useListOrdersQuery } from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { Card, CardHeader } from '../../../components/ui/Card'
import { Combobox } from '../../../components/ui/Combobox'
import { EmptyState } from '../../../components/ui/EmptyState'
import { Input } from '../../../components/ui/Input'
import { MonthStepper } from '../../../components/ui/MonthStepper'
import { Select } from '../../../components/ui/Select'
import { currentMonth, monthRange, todayInManila } from '../../../lib/dates'
import { formatPeso } from '../../../lib/format'
import { PAYMENT_STATUSES, type PaymentStatus } from '../../../types/api'
import { OrdersTable } from './OrdersTable'

const PAGE_SIZE = 25

const MONTH = /^\d{4}-(0[1-9]|1[0-2])$/
const DATE = /^\d{4}-\d{2}-\d{2}$/

/**
 * The period on Orders lives in the URL (handover 18), so a refresh or a
 * shared link keeps it: `?month=2026-10` for a month, `?from=&to=` for a
 * custom range, nothing for this month. An invalid or future month falls back
 * to this month. Customer, status and page stay in the screen's own state.
 */
function usePeriod() {
  const [params, setParams] = useSearchParams()
  const today = todayInManila()
  const thisMonth = currentMonth(today)
  const qFrom = params.get('from') ?? ''
  const qTo = params.get('to') ?? ''
  const custom = DATE.test(qFrom) && DATE.test(qTo)
  const qMonth = params.get('month') ?? ''
  const month = custom ? currentMonth(qFrom) : MONTH.test(qMonth) && `${qMonth}-01` <= thisMonth ? `${qMonth}-01` : thisMonth
  const [from, to] = custom ? [qFrom, qTo] : monthRange(month)

  // Arrows push (Back walks the months); typing a date replaces.
  const goMonth = (target: string) =>
    setParams((current) => {
      const next = new URLSearchParams(current)
      next.delete('from')
      next.delete('to')
      if (target === thisMonth) next.delete('month')
      else next.set('month', target.slice(0, 7))
      return next
    })
  const setRange = (rangeFrom: string, rangeTo: string) =>
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        next.delete('month')
        next.set('from', rangeFrom)
        next.set('to', rangeTo)
        return next
      },
      { replace: true },
    )

  return { today, thisMonth, custom, month, from, to, goMonth, setRange }
}

export function OrdersPage() {
  const navigate = useNavigate()
  const period = usePeriod()
  const { from, to } = period
  // Page and the typed custom dates belong to one period: when the period
  // changes any way (arrows, This month, the browser's Back), they start over.
  const periodKey = `${from}|${to}`
  const [showCustom, setShowCustom] = useState(period.custom)
  // The custom range as typed: applied once both dates are complete and in order.
  const [typed, setTyped] = useState({ key: periodKey, from, to })
  const draft = typed.key === periodKey ? typed : { from, to }
  const setDraft = (next: { from: string; to: string }) => setTyped({ key: periodKey, ...next })
  const [customerId, setCustomerId] = useState<string | null>(null)
  const [status, setStatus] = useState<PaymentStatus | ''>('')
  const [paging, setPaging] = useState({ key: periodKey, page: 1 })
  const page = paging.key === periodKey ? paging.page : 1
  const setPage = (next: number) => setPaging({ key: periodKey, page: next })

  const { data: customers } = useListCustomersQuery({ includeInactive: true })
  const { data, isLoading, isFetching } = useListOrdersQuery({
    from,
    to,
    customerId: customerId ?? undefined,
    status: status || undefined,
    page,
    pageSize: PAGE_SIZE,
  })
  const pages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / PAGE_SIZE))

  // Any filter change starts again at page 1.
  const filter = <T,>(set: (value: T) => void) => (value: T) => {
    set(value)
    setPage(1)
  }
  // Changing the period resets the page and the typed dates by itself (periodKey).
  const goMonth = (month: string) => period.goMonth(month)
  const editRange = (next: { from: string; to: string }) => {
    setDraft(next)
    if (!DATE.test(next.from) || !DATE.test(next.to) || next.from > next.to) return
    period.setRange(next.from, next.to)
  }

  return (
    <div className="flex flex-col gap-3">
      <Card>
        <CardHeader>
          <div className="flex flex-1 flex-col gap-3">
            <div className="flex flex-wrap items-center gap-3">
              <MonthStepper month={period.month} onChange={goMonth} today={period.today} display={period.custom ? 'Custom' : undefined} />
              {(period.custom || period.month !== period.thisMonth) && (
                <Button size="sm" onClick={() => goMonth(period.thisMonth)}>
                  This month
                </Button>
              )}
              <button
                type="button"
                aria-expanded={showCustom}
                className="cursor-pointer text-sm font-bold text-accent underline-offset-4 hover:underline"
                onClick={() => {
                  setDraft({ from, to })
                  setShowCustom((open) => !open)
                }}
              >
                Custom dates
              </button>
            </div>
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
              {showCustom && (
                <>
                  <Input label="From" type="date" value={draft.from} onChange={(event) => editRange({ ...draft, from: event.target.value })} />
                  <Input label="To" type="date" value={draft.to} onChange={(event) => editRange({ ...draft, to: event.target.value })} />
                </>
              )}
              <Combobox
                label="Customer"
                placeholder="Anyone"
                options={[
                  { value: '', label: 'Anyone' },
                  ...(customers ?? []).map((c) => ({ value: c.id, label: c.name, hint: c.nickname ?? undefined })),
                ]}
                value={customerId ?? ''}
                onChange={(value) => filter(setCustomerId)(value || null)}
              />
              <Select label="Status" value={status} onChange={(event) => filter(setStatus)(event.target.value as PaymentStatus | '')}>
                <option value="">Any</option>
                {PAYMENT_STATUSES.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </Select>
            </div>
          </div>
          <Button variant="primary" size="sm" className="self-start" onClick={() => navigate('/sales/orders/new')}>
            New order
          </Button>
        </CardHeader>

        <div className={isFetching && !isLoading ? 'opacity-60' : undefined}>
          <OrdersTable orders={data?.items ?? []} loading={isLoading} />
        </div>
        {!isLoading && (data?.items.length ?? 0) === 0 && <EmptyState message="No orders match these filters." />}

        {pages > 1 && (
          <div className="flex items-center justify-end gap-3 px-4 py-3 text-sm">
            <Button size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
              Previous
            </Button>
            <span className="text-muted">
              Page {page} of {pages}
            </span>
            <Button size="sm" disabled={page >= pages} onClick={() => setPage(page + 1)}>
              Next
            </Button>
          </div>
        )}
      </Card>

      {/* For the whole filter, not just this page (the API totals it). Hidden on an API from before handover 18. */}
      {data && data.total !== undefined && data.unpaid !== undefined && (
        <p aria-label="Orders summary" className="flex flex-wrap gap-x-5 gap-y-1 px-1 text-[0.94rem] text-muted">
          <span>
            <b className="text-ink">{data.totalCount}</b> {data.totalCount === 1 ? 'order' : 'orders'}
          </span>
          <span>
            Total <b className="text-ink">{formatPeso(data.total)}</b>
          </span>
          <span>
            Unpaid <b className="text-ink">{formatPeso(data.unpaid)}</b>
          </span>
        </p>
      )}
    </div>
  )
}
