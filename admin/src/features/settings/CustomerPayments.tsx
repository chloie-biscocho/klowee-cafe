import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { z } from 'zod'
import { useUpdateSettingsMutation } from '../../api/settingsApi'
import {
  useCreatePaymentMethodMutation,
  useDeletePaymentMethodMutation,
  useListPaymentMethodsQuery,
  useSetPaymentMethodActiveMutation,
  useUpdatePaymentMethodMutation,
} from '../../api/tabsApi'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { ImageUpload } from '../../components/ui/ImageUpload'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { Select } from '../../components/ui/Select'
import { Textarea } from '../../components/ui/Textarea'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import {
  PAYMENT_CHANNELS,
  PAYMENT_CHANNEL_LABELS,
  TAB_NOTE_KEY,
  type PaymentMethodDto,
  type PaymentMethodRequest,
} from '../../types/api'

/** Transfers only: cash is paid in person, not from the tab page. */
const TRANSFER_CHANNELS = PAYMENT_CHANNELS.filter((channel) => channel !== 'Cash')

const methodSchema = z.object({
  channel: z.enum(PAYMENT_CHANNELS),
  label: z.string().trim().min(1, 'Name it as customers will see it, e.g. "GCash".').max(40, 'At most 40 characters.'),
  accountName: z.string().trim().min(1, 'Whose account it is.').max(120, 'At most 120 characters.'),
  accountNumber: z.string().trim().max(60, 'At most 60 characters.'),
  qrUrl: z.string().nullable(),
  isActive: z.boolean(),
  sortOrder: z.coerce.number<string | number>().int('A whole number.').min(0, 'Zero or more.'),
})

type MethodInput = z.input<typeof methodSchema>
type MethodValues = z.output<typeof methodSchema>

function toRequest(values: MethodValues): PaymentMethodRequest {
  return { ...values, accountNumber: values.accountNumber === '' ? null : values.accountNumber }
}

function MethodModal({ method, nextSort, onClose }: { method: PaymentMethodDto | null; nextSort: number; onClose: () => void }) {
  const [create, { isLoading: isCreating }] = useCreatePaymentMethodMutation()
  const [update, { isLoading: isUpdating }] = useUpdatePaymentMethodMutation()
  const run = useToastedAction()
  const { register, handleSubmit, control, formState } = useForm<MethodInput, unknown, MethodValues>({
    resolver: zodResolver(methodSchema),
    defaultValues: method
      ? { ...method, accountNumber: method.accountNumber ?? '' }
      : { channel: 'GCash', label: '', accountName: '', accountNumber: '', qrUrl: null, isActive: true, sortOrder: nextSort },
  })
  const errors = formState.errors

  const submit = handleSubmit(async (values) => {
    const outcome = method
      ? await run(update({ id: method.id, body: toRequest(values) }), 'Payment method saved.')
      : await run(create(toRequest(values)), 'Payment method added.')
    if (outcome.ok) onClose()
  })

  return (
    <Modal
      open
      title={method ? `Edit ${method.label}` : 'New way to pay'}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose} disabled={isCreating || isUpdating}>
            Cancel
          </Button>
          <Button variant="primary" loading={isCreating || isUpdating} onClick={submit}>
            Save
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-3">
        <div className="grid grid-cols-2 gap-3">
          <Select label="Channel" hint="What a confirmed payment is recorded as." error={errors.channel?.message} {...register('channel')}>
            {TRANSFER_CHANNELS.map((channel) => (
              <option key={channel} value={channel}>
                {PAYMENT_CHANNEL_LABELS[channel]}
              </option>
            ))}
          </Select>
          <Input label="Label" placeholder="GCash" hint="The tab on the customer's page." error={errors.label?.message} {...register('label')} />
        </div>
        <Input label="Account name" error={errors.accountName?.message} {...register('accountName')} />
        <Input
          label="Account number"
          hint="Optional. Customers get a Copy button for it."
          error={errors.accountNumber?.message}
          {...register('accountNumber')}
        />
        <Controller
          control={control}
          name="qrUrl"
          render={({ field }) => <ImageUpload label="QR code" folder="site" value={field.value} onChange={field.onChange} />}
        />
        <div className="grid grid-cols-2 items-end gap-3">
          <Input label="Order" type="number" min={0} hint="Lower shows first." error={errors.sortOrder?.message} {...register('sortOrder')} />
          <label className="flex h-8 items-center gap-2 text-sm">
            <input type="checkbox" className="size-4 accent-accent" {...register('isActive')} />
            Shown on tab pages
          </label>
        </div>
      </div>
    </Modal>
  )
}

/** The line above the ways to pay on every tab page; saved on its own. */
function NoteEditor({ initial }: { initial: string }) {
  const [updateSettings, { isLoading }] = useUpdateSettingsMutation()
  const run = useToastedAction()
  const [baseline, setBaseline] = useState(initial)
  const [note, setNote] = useState(initial)

  return (
    <div className="flex flex-col gap-2">
      <Textarea
        label="Note on the tab page"
        hint="Shown to every customer above the ways to pay. Leave empty for none."
        rows={2}
        maxLength={4000}
        value={note}
        onChange={(event) => setNote(event.target.value)}
      />
      <div>
        <Button
          size="sm"
          loading={isLoading}
          disabled={note === baseline}
          onClick={async () => {
            const outcome = await run(updateSettings([{ key: TAB_NOTE_KEY, value: note }]), 'Note saved.')
            if (outcome.ok) setBaseline(note)
          }}
        >
          Save note
        </Button>
      </div>
    </div>
  )
}

/**
 * Settings → Customer payments (handover 17): what customers see when they
 * open their tab page to pay. Saved apart from the homepage settings above.
 */
export function CustomerPayments({ note }: { note: string }) {
  const { data: methods, isLoading } = useListPaymentMethodsQuery()
  const [setActive] = useSetPaymentMethodActiveMutation()
  const [remove, { isLoading: isDeleting }] = useDeletePaymentMethodMutation()
  const run = useToastedAction()
  const [editing, setEditing] = useState<PaymentMethodDto | 'new' | null>(null)
  const [pendingDelete, setPendingDelete] = useState<PaymentMethodDto | null>(null)
  const nextSort = Math.max(0, ...(methods ?? []).map((m) => m.sortOrder + 1))

  return (
    <Card className="mb-4">
      <CardHeader>
        <h2 className="text-[1.02rem] font-bold">Customer payments</h2>
        <Button size="sm" variant="primary" onClick={() => setEditing('new')}>
          Add a way to pay
        </Button>
      </CardHeader>
      <div className="flex flex-col gap-4 p-4">
        <NoteEditor initial={note} />

        {!isLoading && methods?.length === 0 && (
          <p className="text-sm text-muted">No ways to pay yet. Until you add one, tab pages ask customers to pay in person.</p>
        )}
        <ul className="divide-y divide-line rounded-2xl border border-line empty:hidden">
          {methods?.map((method) => (
            <li key={method.id} className="flex flex-wrap items-center gap-3 px-3 py-2.5">
              {method.qrUrl ? (
                <img src={method.qrUrl} alt="" className="size-12 rounded border border-line object-cover" />
              ) : (
                <span className="flex size-12 items-center justify-center rounded border border-dashed border-line text-[10px] text-muted">no QR</span>
              )}
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium">
                  {method.label} <span className="text-xs font-normal text-muted">· {PAYMENT_CHANNEL_LABELS[method.channel]}</span>
                </p>
                <p className="truncate text-xs text-muted">
                  {method.accountName}
                  {method.accountNumber && ` · ${method.accountNumber}`}
                </p>
              </div>
              {method.isActive ? <Badge tone="success">Shown</Badge> : <Badge>Hidden</Badge>}
              <div className="flex gap-1.5">
                <Button size="sm" onClick={() => setEditing(method)}>
                  Edit
                </Button>
                <Button
                  size="sm"
                  onClick={() =>
                    run(setActive({ id: method.id, isActive: !method.isActive }), method.isActive ? `${method.label} hidden.` : `${method.label} shown.`)
                  }
                >
                  {method.isActive ? 'Hide' : 'Show'}
                </Button>
                <Button size="sm" variant="danger" onClick={() => setPendingDelete(method)}>
                  Delete
                </Button>
              </div>
            </li>
          ))}
        </ul>
      </div>

      {editing && <MethodModal method={editing === 'new' ? null : editing} nextSort={nextSort} onClose={() => setEditing(null)} />}
      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete way to pay"
        message={`Delete ${pendingDelete?.label}? Reports already made with it keep their channel.`}
        loading={isDeleting}
        onCancel={() => setPendingDelete(null)}
        onConfirm={async () => {
          if (!pendingDelete) return
          await run(remove(pendingDelete.id), 'Payment method deleted.')
          setPendingDelete(null)
        }}
      />
    </Card>
  )
}
