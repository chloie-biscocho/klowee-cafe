import { useRef, useState, type KeyboardEvent } from 'react'
import { flushSync } from 'react-dom'
import { useNavigate } from 'react-router'
import { useCreateOrderMutation, useUpdateOrderMutation } from '../../../api/salesApi'
import { Card } from '../../../components/ui/Card'
import { Input } from '../../../components/ui/Input'
import { Textarea } from '../../../components/ui/Textarea'
import { UnsavedChangesPrompt } from '../../../components/ui/UnsavedChangesPrompt'
import type { AddOnDto, CustomerSummaryDto, OrderDetailDto } from '../../../types/api'
import { useToastedAction } from '../../toasts/useToastedAction'
import { CustomerFields } from './CustomerFields'
import { DiscountControl } from './DiscountControl'
import { ItemPicker } from './ItemPicker'
import { OrderSummaryPanel } from './OrderSummaryPanel'
import { blockingProblem, preview, previewLines, toRequest, type OrderDraft, type PickerItem } from './orderDraft'

/**
 * The order form (decision 017): a page, not a modal, left column to fill,
 * right column the live total. Focus starts in Customer; choosing one moves
 * focus to the drink filter; Ctrl/⌘+Enter saves from anywhere.
 */
export function OrderForm({
  initial,
  order,
  items,
  addOns,
  customers,
}: {
  initial: OrderDraft
  /** The order being edited, or undefined for a new one. */
  order?: OrderDetailDto
  items: PickerItem[]
  addOns: AddOnDto[]
  customers: CustomerSummaryDto[]
}) {
  const [draft, setDraft] = useState(initial)
  const [saved, setSaved] = useState(false)
  const [createOrder, { isLoading: isCreating }] = useCreateOrderMutation()
  const [updateOrder, { isLoading: isUpdating }] = useUpdateOrderMutation()
  const run = useToastedAction()
  const navigate = useNavigate()
  const filterRef = useRef<HTMLInputElement>(null)

  const lines = previewLines(draft, items)
  const pricing = preview(lines, draft, addOns, order)
  const customer = customers.find((c) => c.id === draft.customerId)
  const keepsOwnReward = order?.discountType === 'LoyaltyFree' && order.customer.id === draft.customerId
  const freeAllowed = (customer?.availableRewards ?? 0) > 0 || keepsOwnReward
  const problem =
    blockingProblem(draft, lines, pricing.subtotal) ??
    (draft.discountType === 'LoyaltyFree' && !freeAllowed ? 'This customer has no free drink available.' : null)
  const dirty = !saved && JSON.stringify(draft) !== JSON.stringify(initial)

  const update = (patch: Partial<OrderDraft>) => setDraft((current) => ({ ...current, ...patch }))

  const save = async () => {
    if (problem) return
    const body = toRequest(draft, lines)
    const outcome = order
      ? await run(updateOrder({ id: order.id, body }), 'Order saved.')
      : await run(createOrder(body), 'Order recorded.')
    if (outcome.ok) {
      // Clean before leaving, or the unsaved-changes prompt would stop us.
      flushSync(() => setSaved(true))
      navigate(`/sales/orders/${outcome.data.id}`)
    }
  }

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
      event.preventDefault()
      void save()
    }
  }

  return (
    <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_320px]" onKeyDown={onKeyDown}>
      <Card className="flex flex-col gap-5 p-5">
        <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_160px]">
          <CustomerFields
            customers={customers}
            customerId={draft.customerId}
            payerId={draft.payerId}
            autoFocus={!order}
            onCustomer={(customerId) => {
              update({ customerId, payerId: draft.payerId === customerId ? null : draft.payerId })
              if (customerId) setTimeout(() => filterRef.current?.focus())
            }}
            onPayer={(payerId) => update({ payerId })}
          />
          <Input label="Date" type="date" value={draft.orderedOn} onChange={(event) => update({ orderedOn: event.target.value })} />
        </div>

        <ItemPicker
          items={items}
          addOns={addOns}
          lines={draft.lines}
          filterRef={filterRef}
          onChange={(id, line) => update({ lines: { ...draft.lines, [id]: line } })}
        />

        <DiscountControl
          type={draft.discountType}
          value={draft.discountValue}
          freeAllowed={freeAllowed}
          freeReason={customer ? `${customer.name} has no free drink yet (${customer.loyaltyBalance} stamps).` : 'Choose a customer first.'}
          onChange={(discountType, discountValue) => update({ discountType, discountValue })}
        />

        <Textarea label="Notes" rows={2} value={draft.notes} onChange={(event) => update({ notes: event.target.value })} />
      </Card>

      <OrderSummaryPanel
        lines={lines}
        pricing={pricing}
        addOns={addOns}
        problem={problem}
        saving={isCreating || isUpdating}
        onSave={() => void save()}
        onCancel={() => navigate(order ? `/sales/orders/${order.id}` : '/sales/orders')}
      />

      <UnsavedChangesPrompt when={dirty} message="This order hasn't been saved. Leave and lose it?" />
    </div>
  )
}
