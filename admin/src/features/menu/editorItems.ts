import type { MenuVersionItemDto, MenuVersionItemRequest } from '../../types/api'

/**
 * A version item while it is being edited. `price` is a string because that is
 * what an input holds: a half-typed "12." is a real state, and forcing it
 * through `number` would fight the owner's keystrokes. It becomes a number
 * again on the way out, in `toRequestItems`.
 */
export interface EditorItem {
  menuItemId: string
  name: string
  categoryName: string
  price: string
  isAvailable: boolean
  isFeatured: boolean
}

export function toEditorItems(items: MenuVersionItemDto[]): EditorItem[] {
  return items.map((item) => ({
    menuItemId: item.menuItemId,
    name: item.name,
    categoryName: item.categoryName,
    price: String(item.price),
    isAvailable: item.isAvailable,
    isFeatured: item.isFeatured,
  }))
}

/**
 * The body for `PUT /versions/{id}/items`: the whole list, every time.
 * `sortOrder` is the row's position, so reordering needs no separate call.
 */
export function toRequestItems(items: EditorItem[]): MenuVersionItemRequest[] {
  return items.map((item, index) => ({
    menuItemId: item.menuItemId,
    price: Number(item.price) || 0,
    isAvailable: item.isAvailable,
    isFeatured: item.isFeatured,
    sortOrder: index,
  }))
}

export interface CategoryGroup {
  category: string
  rows: { item: EditorItem; index: number }[]
}

/**
 * Groups rows under their category, in the order the categories first appear —
 * which is the API's order, category sort then item sort. Each row keeps its
 * index into the flat list, so the handlers can address it without searching.
 */
export function groupByCategory(items: EditorItem[]): CategoryGroup[] {
  const groups: CategoryGroup[] = []

  items.forEach((item, index) => {
    const existing = groups.find((group) => group.category === item.categoryName)
    if (existing) {
      existing.rows.push({ item, index })
    } else {
      groups.push({ category: item.categoryName, rows: [{ item, index }] })
    }
  })

  return groups
}

/** Swaps a row with its neighbour inside the same category. */
export function moveWithinCategory(
  items: EditorItem[],
  index: number,
  direction: -1 | 1,
): EditorItem[] {
  const group = groupByCategory(items).find((candidate) =>
    candidate.rows.some((row) => row.index === index),
  )
  if (!group) return items

  const position = group.rows.findIndex((row) => row.index === index)
  const neighbour = group.rows[position + direction]
  if (!neighbour) return items

  const next = [...items]
  const moved = next[index]
  const swapped = next[neighbour.index]
  if (!moved || !swapped) return items

  next[index] = swapped
  next[neighbour.index] = moved
  return next
}
