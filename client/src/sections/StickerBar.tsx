import type { PublicHome } from '../api/types'
import { Sticker } from '../components/Sticker'
import { setting } from '../lib/content'
import { formatDateRange } from '../lib/format'

/**
 * One height for the strip whether it is loading or loaded: two lines of text
 * on a phone, one sticker line from 900px. It sits above everything, so if it
 * grew when the data arrived, the whole page would jump down (layout shift).
 */
const STRIP =
  'mx-auto flex min-h-[4.25rem] max-w-page items-center justify-center px-5 py-2.5 text-center text-base text-navy md:min-h-14 md:px-8'

/**
 * The strip above the nav. One line, chosen in this order: the current
 * announcement, then the next pop-up, then the ticker fallback from settings.
 * With none of the three it renders nothing at all. `home` is null while the
 * page is loading: the strip is drawn empty, holding its place.
 */
export function StickerBar({ home }: { home: PublicHome | null }) {
  if (home === null) {
    return (
      <div className="border-b-2 border-cobalt bg-paper" aria-hidden="true">
        <div className={STRIP} />
      </div>
    )
  }

  const line = pickLine(home)
  if (!line) return null

  const content = line.sticker ? <Sticker>{line.text}</Sticker> : line.text
  const isExternal = line.href !== null && !line.href.startsWith('#')

  return (
    <div className="border-b-2 border-cobalt bg-paper">
      <p className={STRIP}>
        {line.href ? (
          <a
            href={line.href}
            className="underline-offset-4 hover:text-cobalt hover:underline"
            {...(isExternal ? { target: '_blank', rel: 'noopener noreferrer' } : {})}
          >
            {content}
          </a>
        ) : (
          content
        )}
      </p>
    </div>
  )
}

interface Line {
  text: string
  href: string | null
  sticker: boolean
}

function pickLine(home: PublicHome): Line | null {
  if (home.announcement && home.announcement.text.trim() !== '') {
    return { text: home.announcement.text, href: home.announcement.linkUrl, sticker: true }
  }
  if (home.nextEvent) {
    const { name, startsOn, endsOn } = home.nextEvent
    return {
      text: `next pop-up: ${name} · ${formatDateRange(startsOn, endsOn)}`,
      href: '#popups',
      sticker: true,
    }
  }
  const fallback = setting(home, 'ticker_fallback')
  return fallback ? { text: fallback, href: '#book', sticker: false } : null
}
