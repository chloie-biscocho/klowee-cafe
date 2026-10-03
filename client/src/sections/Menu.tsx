import type { PublicHome, PublicMenuItem } from '../api/types'
import { PaperCard } from '../components/PaperCard'
import { PlaceholderThumb } from '../components/Placeholder'
import { Section } from '../components/Section'
import { Sticker } from '../components/Sticker'
import { TentSketch } from '../components/TentSketch'
import { formatPeso } from '../lib/format'

/** The live pop-up menu, one paper card per category. */
export function Menu({ home }: { home: PublicHome }) {
  const categories = (home.menu?.categories ?? []).filter((category) => category.items.length > 0)

  return (
    <Section id="menu" band="paper" heading="what's in the cart">
      {categories.length === 0 ? (
        <PaperCard className="flex flex-col items-center gap-4 px-6 py-12 text-center">
          <TentSketch className="w-40" />
          <p className="font-hand text-3xl font-semibold text-navy">menu coming soon</p>
        </PaperCard>
      ) : (
        <>
          {home.menu?.versionName && (
            <p className="-mt-5 mb-8 font-hand text-2xl font-medium text-ink-muted">
              {home.menu.versionName}
            </p>
          )}
          <div className="grid items-start gap-6 md:grid-cols-2">
            {categories.map((category) => (
              <PaperCard key={category.id} as="article" className="px-5 py-6 md:px-7">
                <h3 className="font-heading text-2xl leading-[1.05] text-cobalt">{category.name}</h3>
                <ul className="mt-3">
                  {category.items.map((item) => (
                    <MenuRow key={item.id} item={item} />
                  ))}
                </ul>
              </PaperCard>
            ))}
          </div>
        </>
      )}
    </Section>
  )
}

function MenuRow({ item }: { item: PublicMenuItem }) {
  return (
    <li className="flex items-start gap-4 border-b-2 border-dashed border-divider py-4 last:border-b-0">
      {item.photoUrl ? (
        <img
          src={item.photoUrl}
          alt={item.name}
          loading="lazy"
          decoding="async"
          className="size-12 shrink-0 rounded-[10px] bg-sky-light object-cover"
        />
      ) : (
        <PlaceholderThumb label={`photo of ${item.name}`} />
      )}
      {/* Price sits under the name on narrow screens and to the right from 480px. */}
      <div className="flex min-w-0 flex-1 flex-col gap-1 sm:flex-row sm:items-start sm:justify-between sm:gap-4">
        <div className="min-w-0">
          <p className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <span className="font-heading text-lg leading-[1.05] text-navy">{item.name}</span>
            {item.isFeatured && <Sticker className="text-lg">best seller</Sticker>}
          </p>
          {item.description && <p className="mt-1 text-sm text-navy/80">{item.description}</p>}
        </div>
        <p className="font-bold text-cobalt sm:shrink-0">{formatPeso(item.price)}</p>
      </div>
    </li>
  )
}
