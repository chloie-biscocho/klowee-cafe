import type { PublicHome } from '../api/types'
import { Clouds } from '../components/Clouds'
import { PaperCard } from '../components/PaperCard'
import { Photo } from '../components/Photo'
import { Pill } from '../components/Pill'
import { Wordmark } from '../components/Wordmark'
import { setting, splitLastWords } from '../lib/content'

export function Hero({ home }: { home: PublicHome }) {
  const heading = setting(home, 'hero_heading')
  const body = setting(home, 'hero_body')
  const [lead, accent] = splitLastWords(heading, 2)

  return (
    <section aria-labelledby="hero-heading" className="relative bg-sky pt-14 pb-16 md:pt-20 md:pb-24">
      <Clouds />
      <div className="relative mx-auto max-w-page px-5 md:px-8">
        <PaperCard tape className="mx-auto max-w-2xl animate-settle px-6 pt-10 pb-8 text-center md:px-12">
          <p className="text-4xl md:text-5xl">
            <Wordmark />
          </p>
          {heading ? (
            <h1
              id="hero-heading"
              className="mt-6 font-heading text-4xl leading-[1.05] text-navy md:text-6xl"
            >
              {lead && <>{lead} </>}
              <span className="text-cobalt">{accent}</span>
            </h1>
          ) : (
            // Every page needs one h1; the business name is the honest default.
            <h1 id="hero-heading" className="sr-only">
              Klowee Cafe
            </h1>
          )}
          {body && <p className="mx-auto mt-5 max-w-prose text-lg text-navy">{body}</p>}
          <div className="mt-8 flex flex-wrap justify-center gap-3">
            <Pill href="#book">Book the cart</Pill>
            <Pill href="#menu" tone="pink">
              See the menu
            </Pill>
          </div>
        </PaperCard>

        <Photo
          url={setting(home, 'hero_image_url')}
          alt={heading || 'Klowee Cafe'}
          placeholder="photo of the cart"
          aspect="hero"
          priority
          className="mt-10 md:mt-14"
        />
      </div>
    </section>
  )
}
