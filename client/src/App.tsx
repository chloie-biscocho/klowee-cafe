import { useHomeData } from './api/useHomeData'
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
          {data && (
            <>
              <CupShelf menu={data.menu} />
              <Story home={data} />
              <Menu home={data} />
              <Popups home={data} />
              <Packages home={data} />
              <Book home={data} />
            </>
          )}
        </main>

        <Footer home={data} />
      </div>
    </div>
  )
}
