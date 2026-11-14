import { useState } from 'react'
import { useCreateTabLinkMutation } from '../../../api/tabsApi'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import { formatManilaDateTime } from '../../../lib/dates'
import type { CustomerDetailDto } from '../../../types/api'
import { useCopy } from '../../toasts/useCopy'
import { useToastedAction } from '../../toasts/useToastedAction'

/**
 * The customer's private "my tab" page (decision 021): create it, copy it to
 * send, or reset it when a link was shared too widely. Resetting kills the
 * old URL at once.
 */
export function TabLinkBox({ customer }: { customer: CustomerDetailDto }) {
  const [createLink, { isLoading }] = useCreateTabLinkMutation()
  const run = useToastedAction()
  const copy = useCopy()
  const [confirmingReset, setConfirmingReset] = useState(false)

  return (
    <Card className="flex flex-col gap-2 p-5">
      <h2 className="text-sm font-semibold">Tab link</h2>
      {customer.tabUrl ? (
        <>
          <div className="flex flex-wrap items-center gap-2">
            <input
              readOnly
              aria-label="Tab link"
              value={customer.tabUrl}
              onFocus={(event) => event.target.select()}
              className="h-8 min-w-0 flex-1 rounded-md border border-line bg-canvas px-2.5 font-mono text-xs"
            />
            <Button size="sm" variant="primary" onClick={() => copy(customer.tabUrl!, 'Link')}>
              Copy
            </Button>
            <Button size="sm" onClick={() => setConfirmingReset(true)}>
              Reset link
            </Button>
          </div>
          {customer.tabLinkCreatedAt && (
            <p className="text-xs text-muted">Created {formatManilaDateTime(customer.tabLinkCreatedAt)}. Anyone with it can see this tab, so send it only to {customer.name}.</p>
          )}
        </>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm text-muted">No link yet. {customer.name}'s private page shows their tab, their cards and how to pay.</p>
          <Button size="sm" variant="primary" loading={isLoading} onClick={() => run(createLink(customer.id), 'Link created.')}>
            Create link
          </Button>
        </div>
      )}

      <ConfirmDialog
        open={confirmingReset}
        title="Reset tab link"
        message={`The old link stops working at once. Send ${customer.name} the new one.`}
        confirmLabel="Reset link"
        loading={isLoading}
        onCancel={() => setConfirmingReset(false)}
        onConfirm={async () => {
          await run(createLink(customer.id), 'New link created. The old one no longer works.')
          setConfirmingReset(false)
        }}
      />
    </Card>
  )
}
