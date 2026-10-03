/**
 * Swaps an entry with its neighbour. Package inclusions and event photos both
 * reorder with ↑/↓ buttons; position in the array becomes `sortOrder` on save.
 * Returns the same array when the move would leave the list.
 */
export function moveItem<T>(items: T[], index: number, direction: -1 | 1): T[] {
  const target = index + direction
  if (target < 0 || target >= items.length) return items
  const next = [...items]
  const moved = next[index] as T
  next[index] = next[target] as T
  next[target] = moved
  return next
}
