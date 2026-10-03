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

/** Still to come, by the API's own rule: Upcoming and not yet over in Manila. */
export function isUpcoming(event: PublicEvent, today: string): boolean {
  return event.status === 'Upcoming' && event.endsOn >= today
}

/** Upcoming events first, soonest first; past ones keep the API's order. */
export function orderEvents(events: PublicEvent[], today: string): PublicEvent[] {
  const upcoming = events
    .filter((event) => isUpcoming(event, today))
    .sort((a, b) => a.startsOn.localeCompare(b.startsOn))
  return [...upcoming, ...events.filter((event) => !isUpcoming(event, today))]
}
