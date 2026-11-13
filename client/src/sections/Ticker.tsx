import type { ReactNode } from 'react'
import type { PublicHome } from '../api/types'
import { tickerLine } from '../lib/content'

const STRIP = 'bg-deep px-4 py-[9px] text-center text-[0.96rem] font-medium text-sky'
const LINK = 'text-sky underline underline-offset-4 focus-visible:outline-sky'

/**
 * The ticker: a slim deep-blue strip above the header with a slowly pulsing
 * dot. It says the current announcement, else the next pop-up, else the
 * owners' fallback line, else it isn't there at all.
 *
 * While the page loads (`home` null) an empty strip of the same height holds
 * its place: production always has at least the fallback line, so the strip
 * nearly always appears, and reserving it keeps the page from jumping down.
 */
export function Ticker({ home }: { home: PublicHome | null }) {
  if (home === null) {
    return (
      <div aria-hidden="true" className={STRIP}>
        {' '}
      </div>
    )
  }

  const line = tickerLine(home)
  if (!line) return null

  let content: ReactNode
  if (line.kind === 'announcement') {
    content = line.href ? (
      <a href={line.href} className={LINK} {...external(line.href)}>
        {line.text}
      </a>
    ) : (
      line.text
    )
  } else if (line.kind === 'next') {
    content = (
      <>
        <b className="font-bold">Next pop-up:</b> {line.name}, {line.dates}, {line.venue}.{' '}
        <a href="#popups" className={LINK}>
          See details
        </a>
      </>
    )
  } else {
    content = (
      <a href="#book" className={LINK}>
        {line.text}
      </a>
    )
  }

  return (
    <div className={STRIP}>
      <p>
        <span aria-hidden="true" className="ticker-dot mr-[9px] inline-block size-2 rounded-full bg-sky align-middle" />
        {content}
      </p>
    </div>
  )
}

const external = (href: string) =>
  href.startsWith('#') ? {} : { target: '_blank', rel: 'noopener noreferrer' }
