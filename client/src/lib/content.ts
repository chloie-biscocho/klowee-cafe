import type { PublicEvent, PublicHome, PublicMenu, PublicMenuCategory } from '../api/types'
import { baseName } from './cupLayers'
import { formatDateRange } from './format'

/** A setting's value, trimmed; `''` when the key was never saved or is blank. */
export function setting(home: PublicHome | null, key: string): string {
  return home?.settings[key]?.trim() ?? ''
}

/** The owners write paragraphs separated by a blank line. */
export function paragraphs(text: string | null | undefined): string[] {
  return (text ?? '')
    .split(/\n\s*\n/)
    .map((paragraph) => paragraph.trim())
    .filter(Boolean)
}

/**
 * The strip above the header: the current announcement, else the next pop-up,
 * else nothing. Which event is next is the API's call (`nextEvent`).
 */
export function announcementLine(home: PublicHome): { text: string; href: string | null } | null {
  const announcement = home.announcement
  if (announcement && announcement.text.trim() !== '') {
    return { text: announcement.text.trim(), href: announcement.linkUrl }
  }
  if (home.nextEvent) {
    const { name, startsOn, endsOn } = home.nextEvent
    return { text: `next pop-up: ${name}, ${formatDateRange(startsOn, endsOn)}`, href: '#popups' }
  }
  return null
}

/**
 * One cup per drink on the pop-up menu, in menu order, each drink once: the
 * two matcha blends ("Banana Matcha (Houseblend)", "… (Okumidori)") are one
 * drink on the shelf. The API already leaves out unavailable items.
 */
export function shelfDrinks(menu: PublicMenu | null): string[] {
  const seen = new Set<string>()
  const drinks: string[] = []
  for (const category of menu?.categories ?? []) {
    for (const item of category.items) {
      const name = baseName(item.name)
      const key = name.toLowerCase()
      if (seen.has(key)) continue
      seen.add(key)
      drinks.push(name)
    }
  }
  return drinks
}

/**
 * Two columns for the menu sheet, in menu order, split where the item counts
 * (plus one line per category heading) come closest to even.
 */
export function menuColumns(categories: PublicMenuCategory[]): [PublicMenuCategory[], PublicMenuCategory[]] {
  const weights = categories.map((category) => category.items.length + 1)
  const total = weights.reduce((sum, weight) => sum + weight, 0)
  let best = 0
  let bestGap = Infinity
  let running = 0
  for (let split = 0; split <= categories.length; split++) {
    const gap = Math.abs(total - 2 * running)
    if (gap < bestGap) {
      best = split
      bestGap = gap
    }
    running += weights[split] ?? 0
  }
  return [categories.slice(0, best), categories.slice(best)]
}

/**
 * The API's next event leads; everything else keeps the API's order (upcoming
 * first, then the most recent past). Whether it is still upcoming is the API's
 * call (`nextEvent.status`), not a date comparison here.
 */
export function nextFirst(events: PublicEvent[], nextId: string | null): PublicEvent[] {
  const next = events.filter((event) => event.id === nextId)
  return [...next, ...events.filter((event) => event.id !== nextId)]
}

/** The yellow sticker: "next" on the upcoming event, else "latest" on the newest. */
export function polaroidBadge(events: PublicEvent[], nextId: string | null): { id: string; label: string } | null {
  if (nextId && events.some((event) => event.id === nextId)) return { id: nextId, label: 'next' }
  const newest = events[0]
  return newest ? { id: newest.id, label: 'latest' } : null
}

/**
 * "Banana Matcha (Houseblend)" under "Houseblend Matcha" prints as "banana matcha":
 * the oval already says which blend. A parenthetical the category doesn't repeat stays.
 */
export function displayName(itemName: string, categoryName: string): string {
  const note = itemName.match(/\(([^)]*)\)\s*$/)?.[1]?.trim().toLowerCase()
  return note && categoryName.toLowerCase().includes(note) ? baseName(itemName) : itemName
}
