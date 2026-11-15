/** What the cup shelf measures from the page, in px. */
export interface ShelfMeasures {
  /** The whole row: both arrows and the cups between them. */
  row: number
  arrow: number
  /** The gap between an arrow and the cups. */
  rowGap: number
  cup: number
  /** The gap between two cups. */
  gap: number
  /** The cups' scroller padding on each side (room for a tilted cup's corner and its name tag). */
  pad: number
  drinks: number
}

/**
 * How many whole cups fit between the arrows, how wide the visible window is
 * for exactly that many, and how far one arrow press scrolls (one window).
 * Never fewer than two cups, never more than there are drinks.
 */
export function shelfWindow({ row, arrow, rowGap, cup, gap, pad, drinks }: ShelfMeasures) {
  const step = cup + gap
  if (step <= 0) return null
  const room = row - 2 * (arrow + rowGap) - 2 * pad
  const count = Math.max(2, Math.min(drinks, Math.floor((room + gap) / step)))
  return { count, width: count * step - gap + 2 * pad, page: count * step }
}
