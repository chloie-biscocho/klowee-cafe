import type { PublicHome } from '../api/types'
import { FormattedText } from '../components/FormattedText'
import { setting } from '../lib/content'
import { parseFormatted } from '../lib/formatted'

/** White panel: a round photo (or an empty round slot) beside the owners' story. Hidden with no story at all. */
export function Story({ home }: { home: PublicHome }) {
  const heading = setting(home, 'story_heading')
  const body = setting(home, 'story_body')
  const image = setting(home, 'story_image_url')
  if (!heading && parseFormatted(body).length === 0) return null

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
        {/* Full width: the text holds a size container (justified text needs its column width), which reports no content width to a centring grid. */}
        <div className="w-full">
          <h2
            id="story-heading"
            className={heading ? 'mb-1.5 font-serif text-[clamp(1.7rem,4vw,2.3rem)] leading-[1.1] text-deep' : 'sr-only'}
          >
            {heading || 'Our story'}
          </h2>
          <FormattedText text={body} paragraphClassName="mt-3 first-of-type:mt-0" listClassName="mt-3" />
        </div>
      </div>
    </section>
  )
}
