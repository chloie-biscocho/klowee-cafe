import type { PublicHome } from '../api/types'
import { Clouds } from '../components/Clouds'
import { PaperCard } from '../components/PaperCard'
import { Pill } from '../components/Pill'
import { setting } from '../lib/content'

/** Ways to reach the owners. The real booking form is phase 2. */
export function Book({ home }: { home: PublicHome }) {
  const email = setting(home, 'contact_email')
  const instagram = setting(home, 'instagram_url')
  const facebook = setting(home, 'facebook_url')
  const external = { target: '_blank', rel: 'noopener noreferrer' }

  return (
    <section id="book" aria-labelledby="book-heading" className="relative bg-sky py-16 md:py-24">
      <Clouds />
      <div className="relative mx-auto max-w-page px-5 md:px-8">
        <PaperCard tape className="mx-auto max-w-2xl px-6 pt-10 pb-8 text-center md:px-12">
          <h2 id="book-heading" className="font-heading text-4xl leading-[1.05] text-navy md:text-5xl">
            bring the cart to your event
          </h2>
          {(email || instagram || facebook) && (
            <div className="mt-8 flex flex-wrap justify-center gap-3">
              {email && <Pill href={`mailto:${email}`}>Email us</Pill>}
              {instagram && (
                <Pill href={instagram} tone="pink" {...external}>
                  Message on Instagram
                </Pill>
              )}
              {facebook && (
                <Pill href={facebook} tone="pink" {...external}>
                  Facebook
                </Pill>
              )}
            </div>
          )}
        </PaperCard>
      </div>
    </section>
  )
}
