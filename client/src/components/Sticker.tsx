import type { ReactNode } from 'react'

/** The yellow tilted sticker: "best seller" on the menu, "next" or "latest" on a polaroid. */
export function Sticker({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <span
      className={`inline-block -rotate-4 rounded-full border-[1.5px] border-sticker-ink bg-yellow px-2.5 font-hand text-[0.72rem] font-bold whitespace-nowrap text-sticker-ink ${className}`}
    >
      {children}
    </span>
  )
}
