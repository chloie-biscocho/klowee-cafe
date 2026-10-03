/**
 * Date-time handling for the admin app. The business runs on Manila time
 * (decision 006); the API stores instants in UTC. Everything that crosses
 * between the two goes through here — see decision 010.
 *
 * Manila has had a fixed +08:00 offset with no daylight saving since 1978, so
 * converting an owner-typed wall-clock time is plain string work: append the
 * offset. Reading back uses `Intl` with the named zone, so the display is right
 * whatever time zone the laptop is set to.
 */

export const BUSINESS_TIME_ZONE = 'Asia/Manila'
const MANILA_OFFSET = '+08:00'
const MANILA_OFFSET_MS = 8 * 60 * 60 * 1000

const LOCAL_DATE_TIME = /^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})(:\d{2})?$/

/**
 * `<input type="datetime-local">` gives "2026-05-21T20:00" with no zone at all.
 * Reads it as Manila wall-clock time and returns an ISO instant the API can
 * store: "2026-05-21T20:00:00+08:00".
 */
export function manilaLocalToIso(local: string): string {
  const match = LOCAL_DATE_TIME.exec(local)
  if (!match) throw new Error(`Not a datetime-local value: "${local}"`)
  const [, date, time, seconds] = match
  return `${date}T${time}${seconds ?? ':00'}${MANILA_OFFSET}`
}

/**
 * The reverse, for filling an input: any ISO instant ("2026-05-21T12:00:00Z",
 * "…+00:00", "…+08:00") becomes the Manila wall-clock "2026-05-21T20:00".
 */
export function isoToManilaLocal(iso: string): string {
  const instant = Date.parse(iso)
  if (Number.isNaN(instant)) throw new Error(`Not an ISO instant: "${iso}"`)
  return new Date(instant + MANILA_OFFSET_MS).toISOString().slice(0, 16)
}

/** The current Manila wall-clock time, as a datetime-local value. */
export function nowInManilaLocal(now: Date = new Date()): string {
  return isoToManilaLocal(now.toISOString())
}

/** Today's date in Manila, as the API writes a `DateOnly`: "2026-05-21". */
export function todayInManila(now: Date = new Date()): string {
  return nowInManilaLocal(now).slice(0, 10)
}

const dateTimeFormat = new Intl.DateTimeFormat('en-PH', {
  day: 'numeric',
  month: 'short',
  year: 'numeric',
  hour: 'numeric',
  minute: '2-digit',
  timeZone: BUSINESS_TIME_ZONE,
})

/** An API instant shown in Manila time: "May 21, 2026, 8:00 PM". */
export function formatManilaDateTime(iso: string): string {
  return dateTimeFormat.format(new Date(iso))
}
