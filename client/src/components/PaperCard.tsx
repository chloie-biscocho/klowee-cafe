import type { ElementType, ReactNode } from 'react'

/**
 * The brief's paper card: 18px radius and a 2px cobalt bottom edge instead of a
 * shadow. `tape` adds the pink washi strip across the top edge.
 */
export function PaperCard({
  children,
  tape = false,
  as: Tag = 'div',
  className = '',
}: {
  children: ReactNode
  tape?: boolean
  as?: ElementType
  className?: string
}) {
  return (
    <Tag className={`relative rounded-card border-b-2 border-cobalt bg-paper ${className}`}>
      {tape && (
        <span
          aria-hidden="true"
          className="absolute -top-3 left-1/2 h-6 w-24 -translate-x-1/2 -rotate-3 bg-pink/80"
        />
      )}
      {children}
    </Tag>
  )
}
