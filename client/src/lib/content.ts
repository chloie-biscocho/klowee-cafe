import type { PublicEvent, PublicHome } from '../api/types'

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

/** "smol pop-up cafe, big on matcha." → ["smol pop-up cafe, big", "on matcha."] */
export function splitLastWords(text: string, count: number): [string, string] {
  const words = text.trim().split(/\s+/).filter(Boolean)
  const split = Math.max(0, words.length - count)
  return [words.slice(0, split).join(' '), words.slice(split).join(' ')]
}

/**
 * The API's next event leads; everything else keeps the API's order. Which
 * event is next, and whether it is still upcoming, is the API's call
 * (`nextEvent.status`), not a date comparison here.
 */
export function nextFirst(events: PublicEvent[], nextId: string | null): PublicEvent[] {
  const next = events.filter((event) => event.id === nextId)
  return [...next, ...events.filter((event) => event.id !== nextId)]
}
