import { useId, useRef, useState, type KeyboardEvent } from 'react'
import { controlClasses } from './Field'

export interface ComboboxOption {
  value: string
  label: string
  /** Muted text after the label (a nickname, a balance); also searched. */
  hint?: string
}

interface ComboboxProps {
  label: string
  options: ComboboxOption[]
  value: string | null
  onChange: (value: string | null) => void
  placeholder?: string
  autoFocus?: boolean
  /** Rendered as the last row of the list, e.g. "+ New customer". */
  footer?: { label: string; onSelect: (typed: string) => void }
}

/**
 * A searchable select: type to filter, ↑/↓ to move, Enter to choose, Escape to
 * close. The ARIA combobox pattern (role, aria-expanded, aria-activedescendant)
 * so a screen reader announces the highlighted option as the keys move. The
 * options are already in memory, so filtering needs no debounce.
 */
export function Combobox({ label, options, value, onChange, placeholder, autoFocus, footer }: ComboboxProps) {
  const id = useId()
  const listId = `${id}-list`
  const input = useRef<HTMLInputElement>(null)
  const [query, setQuery] = useState<string | null>(null)
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)

  const selected = options.find((option) => option.value === value) ?? null
  const term = (query ?? '').trim().toLowerCase()
  const matches = term === ''
    ? options
    : options.filter((option) => `${option.label} ${option.hint ?? ''}`.toLowerCase().includes(term))
  const rows = matches.length + (footer ? 1 : 0)

  const choose = (index: number) => {
    if (footer && index === matches.length) {
      footer.onSelect(query ?? '')
    } else if (matches[index]) {
      onChange(matches[index].value)
    }
    setQuery(null)
    setOpen(false)
  }

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      setOpen(true)
      setActive((current) => (rows === 0 ? 0 : (current + (event.key === 'ArrowDown' ? 1 : rows - 1)) % rows))
    } else if (event.key === 'Enter' && open && rows > 0) {
      event.preventDefault()
      choose(active)
    } else if (event.key === 'Escape') {
      setQuery(null)
      setOpen(false)
    }
  }

  return (
    <div className="relative flex flex-col gap-1.5">
      <label htmlFor={id} className="text-xs font-semibold text-muted">
        {label}
      </label>
      <input
        ref={input}
        id={id}
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && rows > 0 ? `${id}-opt-${active}` : undefined}
        autoComplete="off"
        autoFocus={autoFocus}
        placeholder={placeholder}
        className={controlClasses}
        value={query ?? selected?.label ?? ''}
        onChange={(event) => {
          setQuery(event.target.value)
          setActive(0)
          setOpen(true)
        }}
        onFocus={() => setOpen(true)}
        onBlur={() => {
          setQuery(null)
          setOpen(false)
        }}
        onKeyDown={onKeyDown}
      />
      {open && (
        <ul
          id={listId}
          role="listbox"
          aria-label={label}
          className="absolute top-full z-30 mt-1 max-h-64 w-full overflow-y-auto rounded-md border border-line bg-surface py-1 shadow-lg"
        >
          {matches.map((option, index) => (
            <li
              key={option.value}
              id={`${id}-opt-${index}`}
              role="option"
              aria-selected={index === active}
              className={`flex cursor-pointer justify-between gap-3 px-2.5 py-1.5 text-sm ${index === active ? 'bg-accent-soft' : ''}`}
              // mousedown, not click: it fires before the input's blur closes the list.
              onMouseDown={(event) => {
                event.preventDefault()
                choose(index)
              }}
              onMouseEnter={() => setActive(index)}
            >
              <span>{option.label}</span>
              {option.hint && <span className="text-xs text-muted">{option.hint}</span>}
            </li>
          ))}
          {matches.length === 0 && !footer && <li className="px-2.5 py-1.5 text-sm text-muted">No matches</li>}
          {footer && (
            <li
              id={`${id}-opt-${matches.length}`}
              role="option"
              aria-selected={active === matches.length}
              className={`cursor-pointer border-t border-line px-2.5 py-1.5 text-sm font-medium text-accent ${active === matches.length ? 'bg-accent-soft' : ''}`}
              onMouseDown={(event) => {
                event.preventDefault()
                choose(matches.length)
              }}
            >
              {footer.label}
            </li>
          )}
        </ul>
      )}
    </div>
  )
}
