import { useState } from 'react'
import { useHomeData } from './api/useHomeData'
import { NO_PACKAGE_CHOICE } from './lib/booking'
import { PageSky } from './components/PageSky'
import { Book } from './sections/Book'
import { CupShelf } from './sections/CupShelf'
import { ErrorState } from './sections/ErrorState'
import { Footer } from './sections/Footer'
import { Header } from './sections/Header'
import { Hero } from './sections/Hero'
import { LoadingState } from './sections/LoadingState'
import { Menu } from './sections/menu/Menu'
import { Packages } from './sections/Packages'
import { Popups } from './sections/Popups'
import { Story } from './sections/Story'
import { Ticker } from './sections/Ticker'

/**
 * The whole site: one request, one page, sections in the design brief's order.
 * No router (the "pages" are anchors) and no store (the payload is read once
 * and passed down; decision 011).
 */
export function App() {
  const home = useHomeData()
  const data = home.status === 'ready' ? home.data : null
  // A package card's "Book this" picks that package in the booking form (handover 20).
  const [choice, setChoice] = useState(NO_PACKAGE_CHOICE)
  const book = (packageId: string) => setChoice((current) => ({ packageId, nonce: current.nonce + 1 }))

  return (
    <div id="top">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:top-3 focus:left-3 focus:z-50 focus:rounded-full focus:bg-deep focus:px-5 focus:py-2 focus:font-bold focus:text-sky"
      >
        Skip to content
      </a>

      {home.status !== 'error' && <Ticker home={data} />}
      <div className="mx-auto max-w-page px-[22px]">
        <Header hasPackages={data ? data.packages.length > 0 : true} />

        <main id="main" tabIndex={-1} className="focus:outline-none">
          {home.status === 'error' ? (
            <ErrorState onRetry={home.retry} />
          ) : (
            <Hero home={data} />
          )}
          {home.status === 'loading' && <LoadingState />}
          {/* The sections after the hero share one sky (handover 19). It arrives with the data,
              so the static shell is unchanged, and it is absolute, so nothing moves when it does. */}
          {data && (
            <div className="relative isolate">
              <PageSky />
              <CupShelf menu={data.menu} />
              <Story home={data} />
              <Menu home={data} />
              <Popups home={data} />
              <Packages home={data} onBook={book} />
              <Book home={data} choice={choice} />
            </div>
          )}
        </main>

        <Footer home={data} />
      </div>
    </div>
  )
}
