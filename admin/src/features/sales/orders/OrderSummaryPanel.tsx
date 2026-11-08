import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { formatPeso } from '../../../lib/format'
import type { PricingResult } from '../../../lib/pricing'
import type { AddOnDto } from '../../../types/api'
import type { PreviewLine } from './orderDraft'

/**
 * The live total. A preview from lib/pricing.ts; the API computes the real
 * one on save and the order detail shows that (decision 017).
 */
export function OrderSummaryPanel({
  lines,
  pricing,
  addOns,
  problem,
  saving,
  onSave,
  onCancel,
}: {
  lines: PreviewLine[]
  pricing: PricingResult
  addOns: AddOnDto[]
  problem: string | null
  saving: boolean
  onSave: () => void
  onCancel: () => void
}) {
  const addOnName = (id: string) => addOns.find((a) => a.id === id)?.name ?? 'Add-on'
  const free = pricing.freeLineIndex === null ? null : lines[pricing.freeLineIndex]

  return (
    <Card className="sticky top-6 flex flex-col gap-3 p-4">
      <h2 className="text-sm font-semibold">Summary</h2>
      {lines.length === 0 ? (
        <p className="text-sm text-muted">No drinks yet.</p>
      ) : (
        <ul className="flex flex-col gap-1.5 text-sm">
          {lines.map(({ item, line }, index) => (
            <li key={item.menuVersionItemId} className="flex justify-between gap-3">
              <span>
                {item.name}
                {line.quantity > 1 && <span className="text-muted"> ×{line.quantity}</span>}
                {line.addOnIds.length > 0 && (
                  <span className="block text-xs text-muted">+ {line.addOnIds.map(addOnName).join(', ')}</span>
                )}
              </span>
              <span className="tabular-nums">{formatPeso(pricing.lineTotals[index] ?? 0)}</span>
            </li>
          ))}
        </ul>
      )}

      <dl className="flex flex-col gap-1 border-t border-line pt-3 text-sm">
        <div className="flex justify-between">
          <dt className="text-muted">Subtotal</dt>
          <dd className="tabular-nums">{formatPeso(pricing.subtotal)}</dd>
        </div>
        {pricing.discountAmount > 0 && (
          <div className="flex justify-between">
            <dt className="text-muted">{free ? `Free drink: ${free.item.name}` : 'Discount'}</dt>
            <dd className="tabular-nums text-success">−{formatPeso(pricing.discountAmount)}</dd>
          </div>
        )}
        <div className="flex justify-between text-base font-bold">
          <dt>Total</dt>
          <dd aria-label="Total" className="tabular-nums">
            {formatPeso(pricing.total)}
          </dd>
        </div>
      </dl>

      {problem && <p className="text-xs text-muted">{problem}</p>}
      <div className="flex gap-2">
        <Button variant="primary" className="flex-1" loading={saving} disabled={problem !== null} onClick={onSave}>
          Save order
        </Button>
        <Button onClick={onCancel}>Cancel</Button>
      </div>
      <p className="text-center text-[11px] text-muted">Ctrl+Enter saves</p>
    </Card>
  )
}
