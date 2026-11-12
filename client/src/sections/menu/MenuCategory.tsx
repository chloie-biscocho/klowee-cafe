import type { CSSProperties } from 'react'
import type { PublicMenuCategory, PublicMenuItem } from '../../api/types'
import { Sticker } from '../../components/Sticker'
import { displayName } from '../../lib/content'
import { formatMenuPrice } from '../../lib/format'

/** One category: its name in a pink marker oval, an optional description, then the drinks. */
export function MenuCategory({ category, index }: { category: PublicMenuCategory; index: number }) {
  return (
    <div className="mb-[26px]">
      <h3
        data-oval
        style={{ '--i': index } as CSSProperties}
        className="relative mx-auto mb-3.5 table px-[34px] py-[11px] text-[1.08rem] font-bold tracking-[0.05em] lowercase"
      >
        <svg aria-hidden="true" className="absolute inset-0 size-full overflow-visible">
          <path className="oval-path" pathLength={1} />
        </svg>
        {category.name}
      </h3>
      {category.description && (
        <p className="mx-auto mb-2.5 max-w-[34ch] text-center text-[0.92rem] text-menu-soft">{category.description}</p>
      )}
      <ul>
        {category.items.map((item) => (
          <li key={item.id}>
            <Drink item={item} name={displayName(item.name, category.name)} />
          </li>
        ))}
      </ul>
    </div>
  )
}

/** With a description the drink opens to show it (plus and minus marker); without one it is a plain row. */
function Drink({ item, name }: { item: PublicMenuItem; name: string }) {
  const line = (
    <>
      <span className="lowercase">
        {name}
        {item.isFeatured && <Sticker className="ml-1.5">best seller</Sticker>}
      </span>
      <span aria-hidden="true" className="min-w-4 flex-1 -translate-y-1 border-b-2 border-dotted border-menu-rule" />
      <span className="font-medium">{formatMenuPrice(item.price)}</span>
    </>
  )

  if (!item.description) {
    return <div className="flex items-baseline gap-2 py-1 pl-[calc(0.8em+0.5rem)]">{line}</div>
  }

  return (
    <details className="drink">
      <summary className="flex cursor-pointer items-baseline gap-2 py-1">{line}</summary>
      <p className="max-w-[38ch] pb-2 pl-[1.3em] text-[0.93rem] text-menu-soft">{item.description}</p>
    </details>
  )
}
