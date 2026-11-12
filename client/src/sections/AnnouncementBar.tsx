import type { PublicHome } from '../api/types'
import { announcementLine } from '../lib/content'

/**
 * The slim deep-blue strip above the header: the current announcement, else
 * the next pop-up (linking to it), else nothing at all.
 */
export function AnnouncementBar({ home }: { home: PublicHome }) {
  const line = announcementLine(home)
  if (!line) return null

  const external = line.href !== null && !line.href.startsWith('#')
  return (
    <div className="bg-deep px-5 py-2 text-center text-[0.95rem] font-medium text-sky">
      {line.href ? (
        <a
          href={line.href}
          className="text-sky underline underline-offset-4"
          {...(external ? { target: '_blank', rel: 'noopener noreferrer' } : {})}
        >
          {line.text}
        </a>
      ) : (
        <p>{line.text}</p>
      )}
    </div>
  )
}
