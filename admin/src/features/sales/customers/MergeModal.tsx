import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useListCustomersQuery, useMergeCustomerMutation } from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { Combobox } from '../../../components/ui/Combobox'
import { Modal } from '../../../components/ui/Modal'
import { useToastedAction } from '../../toasts/useToastedAction'

/**
 * For duplicates from the spreadsheet. Two steps on purpose: pick the
 * customer to keep, then a confirmation that restates both names, because a
 * merge moves every order, payment and stamp and cannot be undone.
 */
export function MergeModal({ customer, onClose }: { customer: { id: string; name: string }; onClose: () => void }) {
  const { data: customers } = useListCustomersQuery({ includeInactive: true })
  const [merge, { isLoading }] = useMergeCustomerMutation()
  const run = useToastedAction()
  const navigate = useNavigate()
  const [targetId, setTargetId] = useState<string | null>(null)
  const [confirming, setConfirming] = useState(false)

  const options = (customers ?? [])
    .filter((other) => other.id !== customer.id)
    .map((other) => ({ value: other.id, label: other.name, hint: other.nickname ?? undefined }))
  const target = options.find((option) => option.value === targetId)

  const confirm = async () => {
    if (!targetId) return
    const outcome = await run(merge({ id: customer.id, targetId }), `${customer.name} merged into ${target?.label}.`)
    if (outcome.ok) {
      onClose()
      navigate(`/sales/customers/${targetId}`, { replace: true })
    }
  }

  return (
    <Modal
      open
      title={`Merge ${customer.name} into…`}
      onClose={onClose}
      footer={
        <>
          <Button onClick={confirming ? () => setConfirming(false) : onClose} disabled={isLoading}>
            {confirming ? 'Back' : 'Cancel'}
          </Button>
          {confirming ? (
            <Button variant="danger" loading={isLoading} onClick={confirm}>
              Merge for good
            </Button>
          ) : (
            <Button variant="primary" disabled={!targetId} onClick={() => setConfirming(true)}>
              Continue
            </Button>
          )}
        </>
      }
    >
      {confirming && target ? (
        <p className="text-sm">
          Move every order, payment and stamp from <strong>{customer.name}</strong> to <strong>{target.label}</strong>,
          then delete <strong>{customer.name}</strong>? This cannot be undone.
        </p>
      ) : (
        <div className="flex flex-col gap-3">
          <p className="text-sm text-muted">Pick the customer to keep. {customer.name}&apos;s history moves to them.</p>
          <Combobox label="Keep" options={options} value={targetId} onChange={setTargetId} placeholder="Search customers" autoFocus />
        </div>
      )}
    </Modal>
  )
}
