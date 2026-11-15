import { useRef, type KeyboardEvent } from 'react'
import { currentMonth, monthLabel, shiftMonth, todayInManila } from '../../lib/dates'

/**
 * The one month control (handover 18), shared by Dashboard and Orders:
 * previous, "November 2026", next. Next is disabled on the current month in
 * the business time zone. Left and Right arrows step while either button has
 * focus. `month` is the first day of the month ("2026-11-01"); `display`
 * replaces the month's name (Orders shows "Custom" for a custom range).
 */
export function MonthStepper({
  month,
  onChange,
  display,
  today = todayInManila(),
}: {
  month: string
  onChange: (month: string) => void
  display?: string
  today?: string
}) {
  const previous = useRef<HTMLButtonElement>(null)
  const next = useRef<HTMLButtonElement>(null)
  const latest = currentMonth(today)
  const atLatest = month >= latest

  const step = (by: number) => {
    const target = shiftMonth(month, by)
    if (target > latest) return
    // Next is about to disable under the keyboard: keep focus on the control.
    if (target >= latest && document.activeElement === next.current) previous.current?.focus()
    onChange(target)
  }

  const onKeyDown = (event: KeyboardEvent) => {
    const by = { ArrowLeft: -1, ArrowRight: 1 }[event.key]
    if (!by) return
    event.preventDefault()
    step(by)
  }

  const arrow =
    'grid size-[38px] cursor-pointer place-content-center rounded-full bg-surface text-accent shadow-[inset_0_0_0_1.5px_var(--color-line)] disabled:cursor-not-allowed disabled:opacity-40'

  return (
    <div role="group" aria-label="Month" className="inline-flex items-center gap-1.5" onKeyDown={onKeyDown}>
      <button ref={previous} type="button" aria-label="Previous month" className={arrow} onClick={() => step(-1)}>
        <svg viewBox="0 0 24 24" className="size-4" fill="none" stroke="currentColor" strokeWidth="2.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <path d="M15 5l-7 7 7 7" />
        </svg>
      </button>
      <strong aria-live="polite" className="min-w-[9.5em] text-center text-[1.05rem]">
        {display ?? monthLabel(month)}
      </strong>
      <button ref={next} type="button" aria-label="Next month" className={arrow} disabled={atLatest} onClick={() => step(1)}>
        <svg viewBox="0 0 24 24" className="size-4" fill="none" stroke="currentColor" strokeWidth="2.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <path d="M9 5l7 7-7 7" />
        </svg>
      </button>
    </div>
  )
}
