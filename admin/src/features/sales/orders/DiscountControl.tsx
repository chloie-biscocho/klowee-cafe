import type { DiscountType } from '../../../types/api'

const OPTIONS: { value: DiscountType; label: string }[] = [
  { value: 'None', label: 'None' },
  { value: 'Percent', label: 'Percent' },
  { value: 'Fixed', label: 'Fixed' },
]

/**
 * None / Percent / Fixed, on the whole order. Loyalty rewards are not here:
 * they sit on a drink's line (decision 020) and are priced before this.
 */
export function DiscountControl({
  type,
  value,
  onChange,
}: {
  type: DiscountType
  value: string
  onChange: (type: DiscountType, value: string) => void
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <span id="discount-label" className="text-xs font-semibold text-muted">
        Discount
      </span>
      <div className="flex flex-wrap items-center gap-3">
        <div role="radiogroup" aria-labelledby="discount-label" className="inline-flex rounded-md border border-line p-0.5">
          {OPTIONS.map((option) => {
            return (
              <button
                key={option.value}
                type="button"
                role="radio"
                aria-checked={type === option.value}
                className={`rounded px-3 py-1 text-sm disabled:cursor-not-allowed disabled:opacity-40 ${
                  type === option.value ? 'bg-accent text-white' : 'text-ink hover:bg-canvas'
                }`}
                onClick={() => onChange(option.value, option.value === type ? value : '')}
              >
                {option.label}
              </button>
            )
          })}
        </div>
        {(type === 'Percent' || type === 'Fixed') && (
          <label className="flex items-center gap-1.5 text-sm">
            <span className="sr-only">{type === 'Percent' ? 'Percent off' : 'Pesos off'}</span>
            {type === 'Fixed' && <span className="text-muted">₱</span>}
            <input
              type="number"
              min="0"
              max={type === 'Percent' ? 100 : undefined}
              step={type === 'Percent' ? '0.5' : '1'}
              autoFocus
              className="h-8 w-24 rounded-md border border-line px-2 text-sm"
              value={value}
              onChange={(event) => onChange(type, event.target.value)}
            />
            {type === 'Percent' && <span className="text-muted">%</span>}
          </label>
        )}
      </div>
    </div>
  )
}
