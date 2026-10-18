import { useHomeData } from './api/useHomeData'
import { Book } from './sections/Book'
import { ErrorState } from './sections/ErrorState'
import { Footer } from './sections/Footer'
import { Hero } from './sections/Hero'
import { LoadingState } from './sections/LoadingState'
import { Menu } from './sections/Menu'
import { Nav } from './sections/Nav'
import { Packages } from './sections/Packages'
import { Popups } from './sections/Popups'
import { StickerBar } from './sections/StickerBar'
import { Story } from './sections/Story'

/**
 * The whole site: one request, one page, sections in the brief's order. No
 * router — the "pages" are anchors — and no store — the payload is read once
 * and passed down.
 */
export function App() {
  const home = useHomeData()
  const data = home.status === 'ready' ? home.data : null

  return (
    <div id="top">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:top-3 focus:left-3 focus:z-50 focus:rounded-full focus:bg-cobalt focus:px-5 focus:py-2 focus:font-bold focus:text-paper"
      >
        Skip to content
      </a>

      {home.status !== 'error' && <StickerBar home={data} />}
      <Nav hasPackages={data ? data.packages.length > 0 : true} />

      <main id="main" tabIndex={-1} className="focus:outline-none">
        {home.status === 'loading' && <LoadingState />}
        {home.status === 'error' && <ErrorState onRetry={home.retry} />}
        {data && (
          <>
            <Hero home={data} />
            <Story home={data} />
            <Menu home={data} />
            <Packages home={data} />
            <Popups home={data} />
            <Book home={data} />
          </>
        )}
      </main>

      <Footer home={data} />
    </div>
  )
}
