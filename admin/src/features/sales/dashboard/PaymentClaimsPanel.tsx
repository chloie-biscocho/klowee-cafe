import { useState } from 'react'
import { Link } from 'react-router'
import { useConfirmClaimMutation, useListPaymentClaimsQuery, useRejectClaimMutation } from '../../../api/tabsApi'
import { Button } from '../../../components/ui/Button'
import { Card, CardHeader } from '../../../components/ui/Card'
import { Modal } from '../../../components/ui/Modal'
import { Table, Td, Th } from '../../../components/ui/Table'
import { Textarea } from '../../../components/ui/Textarea'
import { formatManilaDateTime } from '../../../lib/dates'
import { formatPeso } from '../../../lib/format'
import { PAYMENT_CHANNEL_LABELS, type PaymentClaimDto } from '../../../types/api'
import { useToastedAction } from '../../toasts/useToastedAction'

/** Why a claim is rejected; the customer reads it on their page. */
function RejectModal({ claim, onClose }: { claim: PaymentClaimDto; onClose: () => void }) {
  const [reject, { isLoading }] = useRejectClaimMutation()
  const run = useToastedAction()
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | undefined>()

  const submit = async () => {
    if (reason.trim() === '') {
      setError('Say why, so the customer knows what to do.')
      return
    }
    const outcome = await run(reject({ id: claim.id, body: { reason: reason.trim() } }), 'Claim rejected.')
    if (outcome.ok) onClose()
  }

  return (
    <Modal
      open
      title={`Reject ${claim.customerName}'s ${formatPeso(claim.amount)}`}
      onClose={onClose}
      width="max-w-md"
      footer={
        <>
          <Button onClick={onClose} disabled={isLoading}>
            Cancel
          </Button>
          <Button variant="danger" loading={isLoading} onClick={submit}>
            Reject
          </Button>
        </>
      }
    >
      <Textarea
        label="Reason"
        hint={`${claim.customerName} sees this on their tab page.`}
        placeholder="No transfer with this reference came in."
        maxLength={500}
        value={reason}
        error={error}
        autoFocus
        onChange={(event) => {
          setReason(event.target.value)
          setError(undefined)
        }}
      />
    </Modal>
  )
}

/**
 * "I've paid" reports from customers' tab pages (decision 021). Check the
 * account, then Confirm (the tab is settled with it, oldest order first) or
 * Reject with a reason. Hidden when nothing is waiting.
 */
export function PaymentClaimsPanel() {
  const { data: claims } = useListPaymentClaimsQuery('Pending')
  const [confirm] = useConfirmClaimMutation()
  const run = useToastedAction()
  const [confirming, setConfirming] = useState<string | null>(null)
  const [rejecting, setRejecting] = useState<PaymentClaimDto | null>(null)

  if (!claims || claims.length === 0) return null

  // Oldest first: the one waiting longest is the one to check first.
  const rows = [...claims].reverse()

  return (
    <Card>
      <CardHeader>
        <h2 className="text-sm font-semibold">Payments to confirm</h2>
        <span className="text-xs text-muted">Check the account for each reference, then confirm.</span>
      </CardHeader>
      <Table>
        <thead>
          <tr>
            <Th>Customer</Th>
            <Th className="text-right">Amount</Th>
            <Th>Channel</Th>
            <Th>Reference</Th>
            <Th>Reported</Th>
            <Th className="text-right">Tab now</Th>
            <Th />
          </tr>
        </thead>
        <tbody>
          {rows.map((claim) => (
            <tr key={claim.id}>
              <Td>
                <Link className="hover:underline" to={`/sales/customers/${claim.customerId}`}>
                  {claim.customerName}
                </Link>
              </Td>
              <Td className="text-right font-semibold tabular-nums">{formatPeso(claim.amount)}</Td>
              <Td>
                {PAYMENT_CHANNEL_LABELS[claim.channel]}
                {claim.paymentMethodLabel && claim.paymentMethodLabel !== PAYMENT_CHANNEL_LABELS[claim.channel] && (
                  <span className="ml-1 text-xs text-muted">({claim.paymentMethodLabel})</span>
                )}
              </Td>
              <Td className="font-mono text-xs">{claim.reference}</Td>
              <Td className="text-muted">{formatManilaDateTime(claim.createdAt)}</Td>
              <Td className={`text-right tabular-nums ${claim.amount > claim.outstanding ? 'text-danger' : ''}`}>
                {formatPeso(claim.outstanding)}
                {claim.amount > claim.outstanding && <span className="block text-xs">less than the claim</span>}
              </Td>
              <Td className="text-right">
                <div className="flex justify-end gap-1.5">
                  <Button
                    size="sm"
                    variant="primary"
                    loading={confirming === claim.id}
                    disabled={confirming !== null}
                    onClick={async () => {
                      setConfirming(claim.id)
                      await run(confirm({ id: claim.id, body: {} }), `Confirmed ${formatPeso(claim.amount)} from ${claim.customerName}.`)
                      setConfirming(null)
                    }}
                  >
                    Confirm
                  </Button>
                  <Button size="sm" disabled={confirming !== null} onClick={() => setRejecting(claim)}>
                    Reject
                  </Button>
                </div>
              </Td>
            </tr>
          ))}
        </tbody>
      </Table>
      {rejecting && <RejectModal claim={rejecting} onClose={() => setRejecting(null)} />}
    </Card>
  )
}
