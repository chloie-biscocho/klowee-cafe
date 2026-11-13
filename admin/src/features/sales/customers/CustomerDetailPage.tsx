import { useState } from 'react'
import { useParams } from 'react-router'
import { useGetCustomerQuery, useUpdateCustomerMutation } from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { EmptyState } from '../../../components/ui/EmptyState'
import { Spinner } from '../../../components/ui/Spinner'
import { formatPeso } from '../../../lib/format'
import { useToastedAction } from '../../toasts/useToastedAction'
import { OrdersTable } from '../orders/OrdersTable'
import { blankToNull, type CustomerValues } from '../schemas'
import { AdjustStampsModal } from './AdjustStampsModal'
import { ClubCard } from './ClubCard'
import { CustomerFormModal } from './CustomerFormModal'
import { LoyaltyLedger } from './LoyaltyLedger'
import { MergeModal } from './MergeModal'
import { SettleModal } from './SettleModal'

type Dialog = 'edit' | 'settle' | 'merge' | 'adjust' | null

export function CustomerDetailPage() {
  const { id = '' } = useParams()
  const { data: customer, isLoading, isError } = useGetCustomerQuery(id)
  const [updateCustomer, { isLoading: isSaving }] = useUpdateCustomerMutation()
  const run = useToastedAction()
  const [dialog, setDialog] = useState<Dialog>(null)
  const [adjustClub, setAdjustClub] = useState<string | undefined>(undefined)
  const [tab, setTab] = useState<'orders' | 'loyalty'>('orders')

  if (isLoading) return <Spinner className="size-6 text-muted" />
  if (isError || !customer) return <EmptyState message="This customer doesn't exist, or was merged into someone else." />

  const save = async (values: CustomerValues) => {
    const outcome = await run(
      updateCustomer({
        id,
        body: { name: values.name, nickname: blankToNull(values.nickname), notes: blankToNull(values.notes), isActive: customer.isActive },
      }),
      'Customer saved.',
    )
    if (outcome.ok) setDialog(null)
  }

  return (
    <div className="flex flex-col gap-4">
      <Card className="flex flex-wrap items-start justify-between gap-6 p-5">
        <div>
          <h2 className="text-lg font-bold">
            {customer.name}
            {customer.nickname && <span className="ml-2 text-sm font-normal text-muted">{customer.nickname}</span>}
            {!customer.isActive && <span className="ml-2 text-sm font-normal text-muted">(inactive)</span>}
          </h2>
          <p className="mt-3 text-xs font-semibold text-muted uppercase">Tab</p>
          <p className={`text-3xl font-bold ${customer.outstandingBalance > 0 ? 'text-danger' : ''}`}>
            {formatPeso(customer.outstandingBalance)}
          </p>
          {customer.notes && <p className="mt-2 max-w-prose text-sm">{customer.notes}</p>}
        </div>
        <div className="flex flex-wrap gap-2">
          <Button onClick={() => setDialog('edit')}>Edit</Button>
          <Button variant="primary" disabled={customer.outstandingBalance <= 0} onClick={() => setDialog('settle')}>
            Settle tab
          </Button>
          <Button variant="danger" onClick={() => setDialog('merge')}>
            Merge into…
          </Button>
        </div>
      </Card>

      {customer.clubs.length > 0 && (
        <Card className="flex flex-col gap-3 p-5">
          <h2 className="text-sm font-semibold">Loyalty cards</h2>
          <div className="grid gap-3 xl:grid-cols-2">
            {customer.clubs.map((club) => (
              <ClubCard
                key={club.clubId}
                customerId={id}
                club={club}
                onAdjust={() => {
                  setAdjustClub(club.clubId)
                  setDialog('adjust')
                }}
              />
            ))}
          </div>
        </Card>
      )}

      <Card>
        <div role="tablist" className="flex gap-1 border-b border-line px-3 pt-2">
          {(['orders', 'loyalty'] as const).map((value) => (
            <button
              key={value}
              type="button"
              role="tab"
              aria-selected={tab === value}
              className={`rounded-t-md px-3 py-2 text-sm ${tab === value ? 'border-b-2 border-accent font-semibold text-accent' : 'text-muted hover:text-ink'}`}
              onClick={() => setTab(value)}
            >
              {value === 'orders' ? 'Orders' : 'Loyalty ledger'}
            </button>
          ))}
        </div>
        {tab === 'orders' ? (
          customer.recentOrders.length === 0 ? (
            <EmptyState message="No orders yet." />
          ) : (
            <OrdersTable orders={customer.recentOrders} />
          )
        ) : (
          <LoyaltyLedger customerId={id} clubs={customer.clubs} />
        )}
      </Card>

      {dialog === 'edit' && <CustomerFormModal customer={customer} saving={isSaving} onClose={() => setDialog(null)} onSubmit={save} />}
      {dialog === 'settle' && (
        <SettleModal customer={{ id, name: customer.name, balance: customer.outstandingBalance }} onClose={() => setDialog(null)} />
      )}
      {dialog === 'adjust' && (
        <AdjustStampsModal customer={customer} clubs={customer.clubs} clubId={adjustClub} onClose={() => setDialog(null)} />
      )}
      {dialog === 'merge' && <MergeModal customer={customer} onClose={() => setDialog(null)} />}
    </div>
  )
}
