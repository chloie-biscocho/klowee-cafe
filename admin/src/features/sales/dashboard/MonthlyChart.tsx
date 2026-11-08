import { useState } from 'react'
import { formatPeso } from '../../../lib/format'
import { niceMax } from './chartScale'

export interface ChartMonth {
  /** "2026-11" */
  key: string
  label: string
  revenue: number
  collected: number
}

const WIDTH = 720
const HEIGHT = 220
const PAD = { top: 12, right: 8, bottom: 26, left: 52 }
const PLOT_W = WIDTH - PAD.left - PAD.right
const PLOT_H = HEIGHT - PAD.top - PAD.bottom

const short = (amount: number) => (amount >= 1000 ? `₱${(amount / 1000).toLocaleString('en-PH', { maximumFractionDigits: 1 })}k` : `₱${amount}`)

/**
 * Revenue and collected for the last twelve months, drawn by hand: a y scale
 * from 0 to a round maximum, one band per month holding two bars, gridlines
 * with labels, and a tooltip for the hovered month. No chart library for one
 * chart (decision 017).
 */
export function MonthlyChart({ months }: { months: ChartMonth[] }) {
  const [hovered, setHovered] = useState<number | null>(null)
  const max = niceMax(Math.max(0, ...months.flatMap((m) => [m.revenue, m.collected])))
  const y = (amount: number) => PAD.top + PLOT_H - (amount / max) * PLOT_H
  const band = PLOT_W / Math.max(1, months.length)
  const bar = Math.min(18, band * 0.32)
  const ticks = [0, 0.25, 0.5, 0.75, 1].map((f) => f * max)
  const hover = hovered === null ? null : months[hovered]

  return (
    <div className="relative">
      <svg
        viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
        className="h-auto w-full"
        role="img"
        aria-label="Revenue and collected, last twelve months"
        onMouseLeave={() => setHovered(null)}
      >
        {ticks.map((tick) => (
          <g key={tick}>
            <line x1={PAD.left} x2={WIDTH - PAD.right} y1={y(tick)} y2={y(tick)} className="stroke-line" />
            <text x={PAD.left - 6} y={y(tick)} dy="0.32em" textAnchor="end" className="fill-muted text-[10px]">
              {short(tick)}
            </text>
          </g>
        ))}
        {months.map((month, index) => {
          const x = PAD.left + index * band
          return (
            <g key={month.key} data-testid={`chart-month-${month.key}`} onMouseEnter={() => setHovered(index)}>
              <rect x={x} y={PAD.top} width={band} height={PLOT_H} className={hovered === index ? 'fill-canvas' : 'fill-transparent'} />
              <rect x={x + band / 2 - bar - 1} y={y(month.revenue)} width={bar} height={PAD.top + PLOT_H - y(month.revenue)} className="fill-accent" rx="2" />
              <rect x={x + band / 2 + 1} y={y(month.collected)} width={bar} height={PAD.top + PLOT_H - y(month.collected)} className="fill-success" rx="2" />
              <text x={x + band / 2} y={HEIGHT - 8} textAnchor="middle" className="fill-muted text-[10px]">
                {month.label}
              </text>
            </g>
          )
        })}
      </svg>

      <div className="mt-1 flex gap-4 text-xs text-muted">
        <span className="flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm bg-accent" /> Revenue
        </span>
        <span className="flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm bg-success" /> Collected
        </span>
      </div>

      {hover && hovered !== null && (
        <div
          role="tooltip"
          className="pointer-events-none absolute top-2 rounded-md border border-line bg-surface px-2.5 py-1.5 text-xs shadow"
          style={{ left: `${((PAD.left + (hovered + 0.5) * band) / WIDTH) * 100}%`, transform: 'translateX(-50%)' }}
        >
          <p className="font-semibold">{hover.label}</p>
          <p>Revenue {formatPeso(hover.revenue)}</p>
          <p>Collected {formatPeso(hover.collected)}</p>
        </div>
      )}

      <table className="sr-only">
        <caption>Revenue and collected by month</caption>
        <tbody>
          {months.map((month) => (
            <tr key={month.key}>
              <th scope="row">{month.label}</th>
              <td>{formatPeso(month.revenue)}</td>
              <td>{formatPeso(month.collected)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
