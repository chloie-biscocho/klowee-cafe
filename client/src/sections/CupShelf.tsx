import { useCallback, useEffect, useRef, useState, type CSSProperties, type PointerEvent } from 'react'
import type { PublicMenu } from '../api/types'
import { shelfDrinks } from '../lib/content'
import { cupLayersFor } from '../lib/cupLayers'
import { shelfWindow } from '../lib/shelf'

/** How long a tapped cup stays lifted on a touch screen, which has no hover to end it. */
export const TAP_LIFT_MS = 1200

/**
 * Every drink on the pop-up menu as a layered cup, each drink once. One row
 * between two arrow buttons: the row is sized to a whole number of cups, so a
 * cup is never cut off at the edge, and it scrolls a page of cups at a time
 * (snapping to cup starts, so swiping lands on whole cups too). No menu, no shelf.
 *
 * Each cup rests at a small tilt, alternating by position. Hovering it (or
 * tapping it, briefly, on a touch screen) straightens and lifts it over a soft
 * shadow and shows its name, with the pop-up polaroids' timing (handover 19).
 * Cups aren't focusable; when the shelf itself has keyboard focus, every cup
 * shows that state, so keyboard users see the names too. Under reduced motion
 * the tilt stays and nothing lifts (index.css).
 */
export function CupShelf({ menu }: { menu: PublicMenu | null }) {
  const drinks = shelfDrinks(menu)
  const row = useRef<HTMLDivElement>(null)
  const shelf = useRef<HTMLDivElement>(null)
  const page = useRef(0)
  const tapTimer = useRef<number | undefined>(undefined)
  const [ends, setEnds] = useState({ atStart: true, atEnd: true })
  const [lifted, setLifted] = useState<number | null>(null)

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
      const shelfStyle = getComputedStyle(shelfEl)
      const fitted = shelfWindow({
        row: rowEl.clientWidth,
        arrow: (rowEl.firstElementChild as HTMLElement | null)?.offsetWidth ?? 0,
        rowGap: parseFloat(getComputedStyle(rowEl).columnGap) || 0,
        cup: (shelfEl.firstElementChild as HTMLElement | null)?.offsetWidth ?? 0,
        gap: parseFloat(shelfStyle.columnGap) || 0,
        pad: parseFloat(shelfStyle.paddingLeft) || 0,
        drinks: drinks.length,
      })
      if (!fitted) return
      shelfEl.style.width = `${fitted.width}px`
      page.current = fitted.page
      updateEnds()
    }
    fit()
    if (typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(fit)
    observer.observe(rowEl)
    return () => observer.disconnect()
  }, [drinks.length, updateEnds])

  useEffect(() => () => window.clearTimeout(tapTimer.current), [])

  if (drinks.length === 0) return null

  const scroll = (direction: 1 | -1) => {
    const reduced = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches
    shelf.current?.scrollBy({ left: direction * page.current, behavior: reduced ? 'auto' : 'smooth' })
  }

  // A mouse lifts a cup while it's over it. A touch has no hover, so a tap lifts it for a moment.
  const enter = (index: number) => (event: PointerEvent) => {
    if (event.pointerType !== 'touch') setLifted(index)
  }
  const leave = (index: number) => (event: PointerEvent) => {
    if (event.pointerType !== 'touch') setLifted((current) => (current === index ? null : current))
  }
  const tap = (index: number) => (event: PointerEvent) => {
    if (event.pointerType !== 'touch') return
    window.clearTimeout(tapTimer.current)
    setLifted(index)
    tapTimer.current = window.setTimeout(() => setLifted(null), TAP_LIFT_MS)
  }

  return (
    <div ref={row} className="flex items-center justify-center gap-2 pt-[30px] pb-1.5 md:gap-3.5">
      <Arrow label="Show earlier drinks" disabled={ends.atStart} onClick={() => scroll(-1)} path="M15 5l-7 7 7 7" />
      <div
        ref={shelf}
        onScroll={updateEnds}
        tabIndex={0}
        role="group"
        aria-label="All drinks, shown as layered cups"
        className="cup-shelf no-scrollbar flex snap-x snap-mandatory scroll-px-3 gap-[13px] overflow-x-auto px-3 pt-2.5 pb-[60px] md:gap-6 md:pb-[50px]"
      >
        {drinks.map((name, index) => {
          const { a, b, c, s1, s2 } = cupLayersFor(name)
          const style = { '--a': a, '--b': b, '--c': c, '--s1': s1, '--s2': s2, '--i': Math.min(index, 10), '--lid': '9px' } as CSSProperties
          return (
            <div
              key={name}
              role="img"
              aria-label={name}
              data-lifted={lifted === index ? '' : undefined}
              onPointerEnter={enter(index)}
              onPointerLeave={leave(index)}
              onPointerDown={tap(index)}
              className={`shelf-cup relative h-[85px] w-[58px] flex-none snap-start md:h-[106px] md:w-[74px] ${index % 2 ? 'cup-tilt-right' : 'cup-tilt-left'}`}
            >
              <div className="cup-shadow" aria-hidden="true" />
              <div className="cup-body absolute inset-0">
                <div className="cup-glass" style={style} />
                <div className="cup-cover" style={style} aria-hidden="true" />
                <div className="absolute -inset-x-[3px] top-0 h-3 rounded-lg bg-cloud" />
              </div>
              <span aria-hidden="true" className="cup-name">
                {name}
              </span>
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
