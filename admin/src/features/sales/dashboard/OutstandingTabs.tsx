import { useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { Button } from '../../../components/ui/Button'
import { Card, CardHeader } from '../../../components/ui/Card'
import { EmptyState } from '../../../components/ui/EmptyState'
import { Td, Th } from '../../../components/ui/Table'
import { formatDate, formatPeso } from '../../../lib/format'
import type { OutstandingRowDto } from '../../../types/api'

/** At most this many rows show at once; more scroll inside the panel (handover 19a; 8 at the owner's request). */
export const VISIBLE_ROWS = 8

const TABLE = 'w-full border-collapse text-sm [&_tbody_tr:last-child_td]:border-b-0'

/**
 * Every customer with a balance, in the API's order, with Settle. Up to 8
 * rows the panel is as tall as its rows. Beyond that it keeps the height of
 * 8 and the rows scroll inside one labelled, focusable region (keyboard
 * users can reach and scroll it), under a header that stays put, with a soft
 * fade at the bottom while there is more below. Sideways scroll on a phone
 * happens inside the same region.
 */
export function OutstandingTabs({
  rows,
  onSettle,
}: {
  rows: OutstandingRowDto[] | undefined
  onSettle: (row: OutstandingRowDto) => void
}) {
  const scrolls = (rows?.length ?? 0) > VISIBLE_ROWS

  return (
    <Card>
      <CardHeader>
        <h2 className="text-[1.02rem] font-bold">Outstanding tabs{rows ? ` (${rows.length})` : ''}</h2>
      </CardHeader>
      {rows && rows.length === 0 ? (
        <EmptyState message="No open tabs. Everyone's paid up." />
      ) : scrolls ? (
        <ScrollRegion>{(sticky) => <Rows rows={rows!} onSettle={onSettle} sticky={sticky} />}</ScrollRegion>
      ) : (
        <div className="overflow-x-auto">
          <Rows rows={rows ?? []} onSettle={onSettle} sticky={false} />
        </div>
      )}
    </Card>
  )
}

function Rows({ rows, onSettle, sticky }: { rows: OutstandingRowDto[]; onSettle: (row: OutstandingRowDto) => void; sticky: boolean }) {
  // A collapsed border doesn't travel with a sticky cell, so the header's rule is drawn as a shadow there.
  const head = sticky ? 'sticky top-0 z-[1] bg-surface border-b-0 shadow-[inset_0_-1.5px_0_var(--color-line)]' : ''
  return (
    <table className={TABLE}>
      <thead>
        <tr>
          <Th className={head}>Customer</Th>
          <Th className={`text-right ${head}`}>Balance</Th>
          <Th className={head}>Oldest unpaid</Th>
          <Th className={head}>
            <span className="sr-only">Actions</span>
          </Th>
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.customerId}>
            <Td>
              <Link className="hover:underline" to={`/sales/customers/${row.customerId}`}>
                {row.name}
              </Link>
            </Td>
            <Td className="text-right font-semibold">{formatPeso(row.balance)}</Td>
            <Td className="text-muted">{formatDate(row.oldestUnpaidOn)}</Td>
            <Td className="text-right">
              <Button size="sm" onClick={() => onSettle(row)}>
                Settle
              </Button>
            </Td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

/** The scrolling box: exactly the header plus 8 rows tall, measured from the rows themselves, so it holds at any width. */
function ScrollRegion({ children }: { children: (sticky: boolean) => ReactNode }) {
  const box = useRef<HTMLDivElement>(null)
  const [height, setHeight] = useState<number | null>(null)
  const [atEnd, setAtEnd] = useState(false)

  useLayoutEffect(() => {
    const element = box.current
    if (!element) return
    const measure = () => {
      const head = element.querySelector('thead')?.getBoundingClientRect().height ?? 0
      const shown = [...element.querySelectorAll('tbody tr')].slice(0, VISIBLE_ROWS)
      const total = head + shown.reduce((sum, row) => sum + row.getBoundingClientRect().height, 0)
      setHeight(total > 0 ? Math.ceil(total) : null)
      setAtEnd(element.scrollTop + element.clientHeight >= element.scrollHeight - 2)
    }
    measure()
    if (typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(measure)
    observer.observe(element)
    const table = element.querySelector('table')
    if (table) observer.observe(table)
    return () => observer.disconnect()
  }, [])

  return (
    <div className="relative">
      <div
        ref={box}
        role="region"
        aria-label="Outstanding tabs, scrollable"
        tabIndex={0}
        data-testid="outstanding-scroll"
        onScroll={(event) => {
          const element = event.currentTarget
          setAtEnd(element.scrollTop + element.clientHeight >= element.scrollHeight - 2)
        }}
        style={height ? { maxHeight: height } : undefined}
        className="overflow-auto rounded-b-[18px] focus-visible:outline-offset-[-3px]"
      >
        {children(true)}
      </div>
      {/* "There's more": a soft fade over the last visible rows, gone at the end. */}
      {!atEnd && (
        <div aria-hidden="true" className="pointer-events-none absolute inset-x-0 bottom-0 h-10 rounded-b-[18px] bg-linear-to-t from-surface to-transparent" />
      )}
    </div>
  )
}
