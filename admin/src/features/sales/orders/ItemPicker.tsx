import { useState, type KeyboardEvent, type Ref } from 'react'
import { formatPeso } from '../../../lib/format'
import type { AddOnDto } from '../../../types/api'
import type { DraftLine, PickerItem } from './orderDraft'

interface ItemPickerProps {
  items: PickerItem[]
  addOns: AddOnDto[]
  lines: Record<string, DraftLine>
  onChange: (menuVersionItemId: string, line: DraftLine) => void
  filterRef?: Ref<HTMLInputElement>
}

/**
 * The drinks, grouped by category. Built for the keyboard: type to filter,
 * ↑/↓ to move the highlight, Enter adds one of the highlighted drink (again
 * for a second). The mouse gets − / + steppers and add-on checkboxes.
 */
export function ItemPicker({ items, addOns, lines, onChange, filterRef }: ItemPickerProps) {
  const [filter, setFilter] = useState('')
  const [active, setActive] = useState(0)

  const term = filter.trim().toLowerCase()
  const visible = term === '' ? items : items.filter((item) => item.name.toLowerCase().includes(term))
  const highlighted = visible[Math.min(active, visible.length - 1)]

  const lineOf = (id: string): DraftLine => lines[id] ?? { quantity: 0, addOnIds: [] }
  const step = (id: string, by: number) => {
    const line = lineOf(id)
    onChange(id, { ...line, quantity: Math.max(0, Math.min(100, line.quantity + by)) })
  }

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      const count = visible.length || 1
      setActive((current) => (Math.min(current, count - 1) + (event.key === 'ArrowDown' ? 1 : count - 1)) % count)
    } else if (event.key === 'Enter' && !event.ctrlKey && !event.metaKey && highlighted) {
      event.preventDefault()
      step(highlighted.menuVersionItemId, 1)
    } else if (event.key === 'Escape') {
      setFilter('')
    }
  }

  const groups = visible.reduce<{ category: string; items: PickerItem[] }[]>((acc, item) => {
    const last = acc[acc.length - 1]
    if (last && last.category === item.category) last.items.push(item)
    else acc.push({ category: item.category, items: [item] })
    return acc
  }, [])

  return (
    <div className="flex flex-col gap-2">
      <input
        ref={filterRef}
        aria-label="Find a drink"
        placeholder="Type to find a drink, Enter to add one"
        className="h-9 w-full rounded-md border border-line px-2.5 text-sm"
        value={filter}
        onChange={(event) => {
          setFilter(event.target.value)
          setActive(0)
        }}
        onKeyDown={onKeyDown}
      />
      <div className="max-h-[28rem] overflow-y-auto rounded-md border border-line">
        {groups.length === 0 && <p className="px-3 py-6 text-center text-sm text-muted">No drink matches “{filter}”.</p>}
        {groups.map((group) => (
          <section key={group.category} aria-label={group.category}>
            <h3 className="sticky top-0 bg-canvas px-3 py-1.5 text-[11px] font-semibold tracking-wide text-muted uppercase">
              {group.category}
            </h3>
            <ul>
              {group.items.map((item) => {
                const line = lineOf(item.menuVersionItemId)
                const isHighlighted = item === highlighted
                const lineAddOns = addOns.filter((a) => a.isActive || line.addOnIds.includes(a.id))
                return (
                  <li
                    key={item.menuVersionItemId}
                    aria-label={item.name}
                    className={`border-t border-line px-3 py-2 ${isHighlighted ? 'bg-accent-soft' : line.quantity > 0 ? 'bg-success-soft/40' : ''}`}
                  >
                    <div className="flex items-center gap-3">
                      <span className={`flex-1 text-sm ${line.quantity > 0 ? 'font-semibold' : ''}`}>{item.name}</span>
                      <span className="w-16 text-right text-sm text-muted">{formatPeso(item.unitPrice)}</span>
                      <div className="flex items-center gap-1">
                        <button
                          type="button"
                          aria-label={`One less ${item.name}`}
                          className="size-7 rounded-md border border-line text-sm disabled:opacity-40"
                          disabled={line.quantity === 0}
                          onClick={() => step(item.menuVersionItemId, -1)}
                        >
                          −
                        </button>
                        <span aria-label={`${item.name} quantity`} className="w-6 text-center text-sm tabular-nums">
                          {line.quantity}
                        </span>
                        <button
                          type="button"
                          aria-label={`One more ${item.name}`}
                          className="size-7 rounded-md border border-line text-sm"
                          onClick={() => step(item.menuVersionItemId, 1)}
                        >
                          +
                        </button>
                      </div>
                    </div>
                    {line.quantity > 0 && lineAddOns.length > 0 && (
                      <div className="mt-1.5 flex flex-wrap gap-x-4 gap-y-1 pl-1">
                        {lineAddOns.map((addOn) => (
                          <label key={addOn.id} className="flex items-center gap-1.5 text-xs text-muted">
                            <input
                              type="checkbox"
                              className="size-3.5 accent-accent"
                              checked={line.addOnIds.includes(addOn.id)}
                              onChange={(event) =>
                                onChange(item.menuVersionItemId, {
                                  ...line,
                                  addOnIds: event.target.checked
                                    ? [...line.addOnIds, addOn.id]
                                    : line.addOnIds.filter((id) => id !== addOn.id),
                                })
                              }
                            />
                            {addOn.name} +{formatPeso(addOn.price)}
                          </label>
                        ))}
                      </div>
                    )}
                  </li>
                )
              })}
            </ul>
          </section>
        ))}
      </div>
    </div>
  )
}
