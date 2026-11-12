import type { PublicHome } from '../api/types'
import { LinkButton } from '../components/Button'
import { setting } from '../lib/content'

/** The centred white panel: how to reach the owners. Each button only when its setting is filled in. */
export function Book({ home }: { home: PublicHome }) {
  const body = setting(home, 'book_body')
  const email = setting(home, 'contact_email')
  const instagram = setting(home, 'instagram_url')
  const facebook = setting(home, 'facebook_url')
  const external = { target: '_blank', rel: 'noopener noreferrer' }

  return (
    <section
      id="book"
      aria-labelledby="book-heading"
      className="mb-[26px] rounded-[22px] bg-panel px-5 py-[26px] text-center md:rounded-[28px] md:px-7 md:pt-12 md:pb-[46px]"
    >
      <h2 id="book-heading" className="mb-1.5 font-serif text-[clamp(1.7rem,4vw,2.3rem)] leading-[1.1] text-deep">
        Bring the cart to your event
      </h2>
      {body && <p className="mx-auto mb-6 max-w-[56ch] text-soft">{body}</p>}
      {(email || instagram || facebook) && (
        <div className="flex flex-wrap justify-center gap-3">
          {email && <LinkButton href={`mailto:${email}`}>Email us</LinkButton>}
          {instagram && (
            <LinkButton href={instagram} variant="ghost" {...external}>
              Message on Instagram
            </LinkButton>
          )}
          {facebook && (
            <LinkButton href={facebook} variant="ghost" {...external}>
              Facebook
            </LinkButton>
          )}
        </div>
      )}
    </section>
  )
}
