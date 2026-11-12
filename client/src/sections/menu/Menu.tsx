import { useRef } from 'react'
import type { PublicHome } from '../../api/types'
import { menuColumns } from '../../lib/content'
import { MenuCategory } from './MenuCategory'
import { useOvals } from './useOvals'

/**
 * The printed pop-up menu on cream paper: two columns balanced by item count
 * (one on phones). No menu published shows "menu coming soon" on the same paper.
 */
export function Menu({ home }: { home: PublicHome }) {
  const sheet = useRef<HTMLElement>(null)
  const categories = (home.menu?.categories ?? []).filter((category) => category.items.length > 0)
  const [left, right] = menuColumns(categories)
  const hasDescriptions = categories.some((category) => category.items.some((item) => item.description))
  useOvals(sheet, home.menu)

  return (
    <section
      ref={sheet}
      id="menu"
      aria-labelledby="menu-heading"
      className="mb-[26px] rounded-md bg-paper px-[18px] pt-7 pb-[18px] font-hand text-menu-ink md:px-10 md:pt-10 md:pb-[30px]"
    >
      <h2 id="menu-heading" className="mb-1.5 text-center text-[1.3rem] font-bold tracking-[0.06em]">
        klowee cafe pop-up menu
      </h2>
      {categories.length === 0 ? (
        <p className="py-10 text-center text-menu-soft">menu coming soon</p>
      ) : (
        <>
          {hasDescriptions && (
            <p className="mb-[26px] text-center text-[0.92rem] text-menu-soft">tap a drink to see what's in it</p>
          )}
          <div className="grid gap-x-14 gap-y-2.5 md:grid-cols-2">
            {[left, right].map((column, side) => (
              <div key={side}>
                {column.map((category) => (
                  <MenuCategory key={category.id} category={category} index={categories.indexOf(category)} />
                ))}
              </div>
            ))}
          </div>
        </>
      )}
    </section>
  )
}
