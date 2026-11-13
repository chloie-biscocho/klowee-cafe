import { useRef, useState, type KeyboardEvent } from 'react'
import { flushSync } from 'react-dom'
import { useNavigate } from 'react-router'
import { useCreateOrderMutation, useUpdateOrderMutation } from '../../../api/salesApi'
import { Card } from '../../../components/ui/Card'
import { Input } from '../../../components/ui/Input'
import { Textarea } from '../../../components/ui/Textarea'
import { UnsavedChangesPrompt } from '../../../components/ui/UnsavedChangesPrompt'
import type { AddOnDto, CustomerSummaryDto, LoyaltyClubDto, OrderDetailDto } from '../../../types/api'
import { useToastedAction } from '../../toasts/useToastedAction'
import { CustomerFields } from './CustomerFields'
import { DiscountControl } from './DiscountControl'
import { ItemPicker } from './ItemPicker'
import { OrderSummaryPanel } from './OrderSummaryPanel'
import { blockingProblem, preview, previewLines, rewardOptions, toRequest, type OrderDraft, type PickerItem } from './orderDraft'

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
  clubs = [],
}: {
  initial: OrderDraft
  /** The order being edited, or undefined for a new one. */
  order?: OrderDetailDto
  items: PickerItem[]
  addOns: AddOnDto[]
  customers: CustomerSummaryDto[]
  /** The loyalty cards, for what each reward takes off. */
  clubs?: LoyaltyClubDto[]
}) {
  const [draft, setDraft] = useState(initial)
  const [saved, setSaved] = useState(false)
  const [createOrder, { isLoading: isCreating }] = useCreateOrderMutation()
  const [updateOrder, { isLoading: isUpdating }] = useUpdateOrderMutation()
  const run = useToastedAction()
  const navigate = useNavigate()
  const filterRef = useRef<HTMLInputElement>(null)

  const lines = previewLines(draft, items)
  const pricing = preview(lines, draft, addOns, order, clubs)
  const customer = customers.find((c) => c.id === draft.customerId)
  const rewardLabel = (rewardId: string) => clubs.flatMap((c) => c.rewards).find((r) => r.id === rewardId)?.label
  const problem = blockingProblem(draft, lines, pricing.subtotal)
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
              // Rewards belong to the drinker: a different drinker starts with none on the lines.
              const lines =
                customerId === draft.customerId
                  ? draft.lines
                  : Object.fromEntries(Object.entries(draft.lines).map(([id, line]) => [id, { ...line, rewardId: null }]))
              update({ customerId, payerId: draft.payerId === customerId ? null : draft.payerId, lines })
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
          rewardsFor={(item) => rewardOptions(item, draft, customer, order)}
          onChange={(id, line) => update({ lines: { ...draft.lines, [id]: line } })}
        />

        {order?.discountType === 'LoyaltyFree' && (
          <p className="rounded-md bg-warning-soft px-3 py-2 text-sm text-warning">
            This order used the old card's free drink, which can't be saved again. It will save with no discount: put a
            reward on the drink's line instead.
          </p>
        )}

        <DiscountControl
          type={draft.discountType}
          value={draft.discountValue}
          onChange={(discountType, discountValue) => update({ discountType, discountValue })}
        />

        <Textarea label="Notes" rows={2} value={draft.notes} onChange={(event) => update({ notes: event.target.value })} />
      </Card>

      <OrderSummaryPanel
        lines={lines}
        pricing={pricing}
        addOns={addOns}
        rewardLabel={rewardLabel}
        problem={problem}
        saving={isCreating || isUpdating}
        onSave={() => void save()}
        onCancel={() => navigate(order ? `/sales/orders/${order.id}` : '/sales/orders')}
      />

      <UnsavedChangesPrompt when={dirty} message="This order hasn't been saved. Leave and lose it?" />
    </div>
  )
}
