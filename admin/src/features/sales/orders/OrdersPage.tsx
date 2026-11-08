import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useListCustomersQuery, useListOrdersQuery } from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { Card, CardHeader } from '../../../components/ui/Card'
import { Combobox } from '../../../components/ui/Combobox'
import { EmptyState } from '../../../components/ui/EmptyState'
import { Input } from '../../../components/ui/Input'
import { Select } from '../../../components/ui/Select'
import { monthRange, todayInManila } from '../../../lib/dates'
import { PAYMENT_STATUSES, type PaymentStatus } from '../../../types/api'
import { OrdersTable } from './OrdersTable'

const PAGE_SIZE = 25

export function OrdersPage() {
  const navigate = useNavigate()
  const [defaultFrom, defaultTo] = monthRange(todayInManila())
  const [from, setFrom] = useState(defaultFrom)
  const [to, setTo] = useState(defaultTo)
  const [customerId, setCustomerId] = useState<string | null>(null)
  const [status, setStatus] = useState<PaymentStatus | ''>('')
  const [page, setPage] = useState(1)

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

  return (
    <Card>
      <CardHeader>
        <div className="grid flex-1 grid-cols-2 gap-3 sm:grid-cols-4">
          <Input label="From" type="date" value={from} onChange={(event) => filter(setFrom)(event.target.value)} />
          <Input label="To" type="date" value={to} onChange={(event) => filter(setTo)(event.target.value)} />
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
        <Button variant="primary" size="sm" className="self-end" onClick={() => navigate('/sales/orders/new')}>
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
  )
}
