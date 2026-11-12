import type { PublicHome } from '../api/types'
import { paragraphs, setting } from '../lib/content'

/** White panel: a round photo (or an empty round slot) beside the owners' story. Hidden with no story at all. */
export function Story({ home }: { home: PublicHome }) {
  const heading = setting(home, 'story_heading')
  const body = paragraphs(setting(home, 'story_body'))
  const image = setting(home, 'story_image_url')
  if (!heading && body.length === 0) return null

  return (
    <section id="story" aria-labelledby="story-heading" className="mb-[26px] rounded-[22px] bg-panel px-5 py-[26px] md:rounded-[28px] md:px-[34px] md:pt-9 md:pb-8">
      <div className="grid justify-items-center gap-5 md:grid-cols-[210px_1fr] md:items-center md:justify-items-stretch md:gap-9">
        {image ? (
          <img
            src={image}
            alt=""
            loading="lazy"
            decoding="async"
            className="size-[170px] rounded-full bg-ph object-cover md:size-[210px]"
          />
        ) : (
          <div aria-hidden="true" className="size-[170px] rounded-full bg-ph md:size-[210px]" />
        )}
        <div>
          <h2
            id="story-heading"
            className={heading ? 'mb-1.5 font-serif text-[clamp(1.7rem,4vw,2.3rem)] leading-[1.1] text-deep' : 'sr-only'}
          >
            {heading || 'Our story'}
          </h2>
          {body.map((paragraph, index) => (
            <p key={index} className="mt-3 first-of-type:mt-0">
              {paragraph}
            </p>
          ))}
        </div>
      </div>
    </section>
  )
}
