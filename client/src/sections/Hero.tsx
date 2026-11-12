import type { PublicHome } from '../api/types'
import { LinkButton } from '../components/Button'
import { Clouds } from '../components/Clouds'
import { setting } from '../lib/content'

/**
 * Drifting clouds, the round logo sticker, the heading and the two calls to
 * action. With `home` null (still loading) the same shell renders with quiet
 * placeholder bars where the text goes, so the hero holds its height and the
 * logo starts loading at once.
 */
export function Hero({ home }: { home: PublicHome | null }) {
  const heading = setting(home, 'hero_heading')
  const body = setting(home, 'hero_body')

  return (
    <section aria-labelledby={home ? 'hero-heading' : undefined} aria-label={home ? undefined : 'Klowee Cafe'} className="relative pt-[30px] pb-3 text-center">
      <Clouds />
      <img
        src="/logo.jpg"
        alt="Klowee Cafe logo: Klowe and Kee in cat beanies"
        width={440}
        height={440}
        fetchPriority="high"
        decoding="async"
        className="relative z-[1] mx-auto mb-[26px] block size-[184px] rounded-full"
      />
      {home === null ? (
        <div aria-hidden="true" className="relative z-[1] mx-auto flex max-w-[44ch] flex-col items-center">
          <div className="h-[clamp(4.24rem,11.9vw,7.63rem)] w-[min(16ch,100%)] rounded-3xl bg-cloud/60 font-serif text-[clamp(2rem,5.6vw,3.6rem)]" />
          <div className="mt-3.5 mb-[22px] h-[3.3em] w-full rounded-2xl bg-cloud/60" />
          <div className="h-12 w-40 rounded-full bg-cloud/60" />
        </div>
      ) : (
        <>
          <h1
            id="hero-heading"
            className={
              heading
                ? 'relative z-[1] mx-auto mb-3.5 max-w-[16ch] font-serif text-[clamp(2rem,5.6vw,3.6rem)] leading-[1.06] text-deep'
                : 'sr-only'
            }
          >
            {heading || 'Klowee Cafe'}
          </h1>
          {body && <p className="relative z-[1] mx-auto mb-[22px] max-w-[44ch]">{body}</p>}
          <div className="relative z-[1]">
            <LinkButton href="#book">Book the cart</LinkButton>
            <a href="#menu" className="mt-3.5 block font-medium text-deep underline md:mt-0 md:ml-4 md:inline">
              See the menu
            </a>
          </div>
        </>
      )}
    </section>
  )
}
