import { useCallback, useEffect, useRef, useState, type CSSProperties } from 'react'
import type { PublicMenu } from '../api/types'
import { shelfDrinks } from '../lib/content'
import { cupLayersFor } from '../lib/cupLayers'

/**
 * Every drink on the pop-up menu as a layered cup, each drink once. One row
 * between two arrow buttons: the row is sized to a whole number of cups, so a
 * cup is never cut off at the edge, and it scrolls a page of cups at a time
 * (snapping to cup starts, so swiping lands on whole cups too). No menu, no shelf.
 */
export function CupShelf({ menu }: { menu: PublicMenu | null }) {
  const drinks = shelfDrinks(menu)
  const row = useRef<HTMLDivElement>(null)
  const shelf = useRef<HTMLDivElement>(null)
  const page = useRef(0)
  const [ends, setEnds] = useState({ atStart: true, atEnd: true })

  const updateEnds = useCallback(() => {
    const el = shelf.current
    if (!el) return
    setEnds({
      atStart: el.scrollLeft < 4,
      atEnd: el.scrollLeft + el.clientWidth > el.scrollWidth - 4,
    })
  }, [])

  // Size the visible row to whole cups whenever the space changes. Layout, not data.
  useEffect(() => {
    const rowEl = row.current
    const shelfEl = shelf.current
    if (!rowEl || !shelfEl || drinks.length === 0) return
    const fit = () => {
      const cup = (shelfEl.firstElementChild as HTMLElement | null)?.offsetWidth ?? 0
      const gap = parseFloat(getComputedStyle(shelfEl).columnGap) || 0
      const arrow = (rowEl.firstElementChild as HTMLElement | null)?.offsetWidth ?? 0
      const rowGap = parseFloat(getComputedStyle(rowEl).columnGap) || 0
      const step = cup + gap
      if (step <= 0) return
      const room = rowEl.clientWidth - 2 * (arrow + rowGap)
      const count = Math.max(2, Math.min(drinks.length, Math.floor((room + gap) / step)))
      shelfEl.style.width = `${count * step - gap + 8}px`
      page.current = count * step
      updateEnds()
    }
    fit()
    if (typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(fit)
    observer.observe(rowEl)
    return () => observer.disconnect()
  }, [drinks.length, updateEnds])

  if (drinks.length === 0) return null

  const scroll = (direction: 1 | -1) => {
    const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
    shelf.current?.scrollBy({ left: direction * page.current, behavior: reduced ? 'auto' : 'smooth' })
  }

  return (
    <div ref={row} className="flex items-center justify-center gap-2 pt-[30px] pb-[34px] md:gap-3.5">
      <Arrow label="Show earlier drinks" disabled={ends.atStart} onClick={() => scroll(-1)} path="M15 5l-7 7 7 7" />
      <div
        ref={shelf}
        onScroll={updateEnds}
        tabIndex={0}
        role="group"
        aria-label="All drinks, shown as layered cups"
        className="no-scrollbar flex snap-x snap-mandatory scroll-px-1 gap-4 overflow-x-auto p-1 md:gap-[30px]"
      >
        {drinks.map((name, index) => {
          const { a, b, c, s1, s2 } = cupLayersFor(name)
          const style = { '--a': a, '--b': b, '--c': c, '--s1': s1, '--s2': s2, '--i': Math.min(index, 10) } as CSSProperties
          return (
            <div
              key={name}
              role="img"
              aria-label={name}
              title={name}
              className="relative h-[106px] w-[72px] flex-none snap-start md:h-[132px] md:w-[92px]"
            >
              <div className="cup-glass" style={style} />
              <div className="absolute -inset-x-1 top-0 h-[15px] rounded-lg bg-cloud" />
            </div>
          )
        })}
      </div>
      <Arrow label="Show more drinks" disabled={ends.atEnd} onClick={() => scroll(1)} path="M9 5l7 7-7 7" />
    </div>
  )
}

/** A real button: focusable, labelled, and disabled (not hidden) at the ends of the row. */
function Arrow({ label, disabled, onClick, path }: { label: string; disabled: boolean; onClick: () => void; path: string }) {
  return (
    <button
      type="button"
      aria-label={label}
      disabled={disabled}
      onClick={onClick}
      className="grid size-9 flex-none cursor-pointer place-content-center rounded-full bg-panel text-deep disabled:cursor-default disabled:opacity-35 md:size-11"
    >
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" className="size-[18px]">
        <path d={path} />
      </svg>
    </button>
  )
}
