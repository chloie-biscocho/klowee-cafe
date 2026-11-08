import { useState } from 'react'
import { useCreateCustomerMutation } from '../../../api/salesApi'
import { Combobox, type ComboboxOption } from '../../../components/ui/Combobox'
import { formatPeso } from '../../../lib/format'
import type { CustomerSummaryDto } from '../../../types/api'
import { useToastedAction } from '../../toasts/useToastedAction'
import { CustomerFormModal } from '../customers/CustomerFormModal'
import { blankToNull } from '../schemas'

/** Active customers, most recent orderers first: the regulars are at the top of the list. */
function options(customers: CustomerSummaryDto[], keep: (string | null)[]): ComboboxOption[] {
  return customers
    .filter((c) => c.isActive || keep.includes(c.id))
    .toSorted((a, b) => (b.lastOrderOn ?? '').localeCompare(a.lastOrderOn ?? '') || a.name.localeCompare(b.name))
    .map((c) => ({ value: c.id, label: c.name, hint: c.nickname ?? undefined }))
}

export function CustomerFields({
  customers,
  customerId,
  payerId,
  autoFocus,
  onCustomer,
  onPayer,
}: {
  customers: CustomerSummaryDto[]
  customerId: string | null
  payerId: string | null
  autoFocus: boolean
  onCustomer: (id: string | null) => void
  onPayer: (id: string | null) => void
}) {
  const [createCustomer, { isLoading: isCreating }] = useCreateCustomerMutation()
  const run = useToastedAction()
  const [newName, setNewName] = useState<string | null>(null)
  const [showPayer, setShowPayer] = useState(payerId !== null)

  const customer = customers.find((c) => c.id === customerId)
  const all = options(customers, [customerId, payerId])

  return (
    <div className="flex flex-col gap-3">
      <Combobox
        label="Customer"
        options={all}
        value={customerId}
        onChange={onCustomer}
        placeholder="Type a name"
        autoFocus={autoFocus}
        footer={{ label: '+ New customer', onSelect: (typed) => setNewName(typed) }}
      />
      {customer && (
        <p className="-mt-1 text-xs text-muted">
          Tab {formatPeso(customer.outstandingBalance)} · {customer.loyaltyBalance} stamps ·{' '}
          <span className={customer.availableRewards > 0 ? 'font-semibold text-success' : ''}>
            {customer.availableRewards} free drink{customer.availableRewards === 1 ? '' : 's'} available
          </span>
        </p>
      )}

      {showPayer ? (
        <div className="flex items-end gap-2">
          <div className="flex-1">
            <Combobox
              label="Paid by"
              options={all.filter((option) => option.value !== customerId)}
              value={payerId}
              onChange={onPayer}
              placeholder="Who covers this order?"
            />
          </div>
          <button
            type="button"
            className="mb-2 text-xs text-muted hover:text-ink"
            onClick={() => {
              onPayer(null)
              setShowPayer(false)
            }}
          >
            Remove
          </button>
        </div>
      ) : (
        <button type="button" className="self-start text-xs text-accent hover:underline" onClick={() => setShowPayer(true)}>
          Paid by someone else?
        </button>
      )}

      {newName !== null && (
        <CustomerFormModal
          customer={null}
          initialName={newName}
          compact
          saving={isCreating}
          onClose={() => setNewName(null)}
          onSubmit={async (values) => {
            const outcome = await run(
              createCustomer({ name: values.name, nickname: blankToNull(values.nickname), notes: null, isActive: true }),
              `${values.name} added.`,
            )
            if (outcome.ok) {
              onCustomer(outcome.data.id)
              setNewName(null)
            }
          }}
        />
      )}
    </div>
  )
}
