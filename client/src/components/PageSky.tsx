import { PAGE_CLOUDS } from '../lib/pageClouds'
import { Clouds } from './Clouds'

/**
 * The sky below the hero: a decorative layer the full width of the window and
 * the height of the sections it sits behind, so the clouds scroll with the
 * page. It never takes part in layout (absolute, behind everything in its
 * isolated parent), takes no pointer events, and clips sideways so a cloud
 * hanging off the edge never adds a scrollbar.
 */
export function PageSky() {
  return (
    <div
      aria-hidden="true"
      data-testid="page-sky"
      className="pointer-events-none absolute inset-y-0 left-[calc(50%-50vw)] -z-10 w-screen overflow-x-clip"
    >
      <Clouds clouds={PAGE_CLOUDS} />
    </div>
  )
}
