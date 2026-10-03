import type { PublicHome } from '../api/types'
import { Photo } from '../components/Photo'
import { paragraphs, setting } from '../lib/content'

/** Illustration left, heading and paragraphs right; stacked on narrow screens. */
export function Story({ home }: { home: PublicHome }) {
  const heading = setting(home, 'story_heading')
  const body = paragraphs(setting(home, 'story_body'))

  return (
    <section id="story" aria-labelledby="story-heading" className="bg-paper py-16 md:py-24">
      <div className="mx-auto grid max-w-page items-center gap-8 px-5 md:grid-cols-2 md:gap-14 md:px-8">
        <Photo
          url={setting(home, 'story_image_url')}
          alt={heading || 'Klowe and Kee'}
          placeholder="Klowe & Kee illustration"
          aspect="square"
        />
        <div className="max-w-prose">
          <h2
            id="story-heading"
            className={heading ? 'font-heading text-4xl leading-[1.05] text-navy md:text-5xl' : 'sr-only'}
          >
            {heading || 'story'}
          </h2>
          {body.map((paragraph, index) => (
            <p key={index} className="mt-4 text-lg text-navy">
              {paragraph}
            </p>
          ))}
        </div>
      </div>
    </section>
  )
}
