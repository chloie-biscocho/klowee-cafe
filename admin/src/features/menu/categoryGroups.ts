import type { MenuItemDto } from '../../types/api'

/**
 * Categories whose items are all archived: these can be deleted (the API
 * refuses only while a live item uses a category), and their items keep their
 * history. An empty category isn't listed; it has nothing archived to explain.
 */
export function archivedOnly(items: MenuItemDto[]): Set<string> {
  const live = new Set(items.filter((i) => !i.isArchived).map((i) => i.categoryId))
  return new Set(items.filter((i) => i.isArchived && !live.has(i.categoryId)).map((i) => i.categoryId))
}
