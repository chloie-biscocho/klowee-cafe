import type { ReactNode } from 'react'

/** Yellow pill, navy border, a handwritten word, tilted 5 to 8 degrees. */
export function Sticker({
  children,
  tilt = 'right',
  className = '',
}: {
  children: ReactNode
  tilt?: 'left' | 'right'
  className?: string
}) {
  return (
    <span
      className={`inline-block rounded-full border-2 border-navy bg-yellow px-3 py-0.5 font-hand text-xl leading-tight font-semibold text-navy ${
        tilt === 'right' ? 'rotate-6' : '-rotate-6'
      } ${className}`}
    >
      {children}
    </span>
  )
}
