import type { ComponentPropsWithoutRef, ReactNode } from 'react'

/** Scrolls sideways on narrow screens instead of stretching the page. */
export function Table({ children }: { children: ReactNode }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-sm">{children}</table>
    </div>
  )
}

export function Th({ className = '', children, ...rest }: ComponentPropsWithoutRef<'th'>) {
  return (
    <th
      className={`border-b border-line px-4 py-2.5 text-left text-xs font-semibold text-muted ${className}`}
      {...rest}
    >
      {children}
    </th>
  )
}

export function Td({ className = '', children, ...rest }: ComponentPropsWithoutRef<'td'>) {
  return (
    <td className={`border-b border-line px-4 py-2.5 align-middle ${className}`} {...rest}>
      {children}
    </td>
  )
}

/** Placeholder rows while a list query is loading, so the table does not jump. */
export function SkeletonRows({ rows = 4, columns }: { rows?: number; columns: number }) {
  return (
    <>
      {Array.from({ length: rows }, (_, rowIndex) => (
        <tr key={rowIndex} aria-hidden>
          {Array.from({ length: columns }, (_, columnIndex) => (
            <Td key={columnIndex}>
              <div className="h-3.5 w-full max-w-40 animate-pulse rounded bg-line" />
            </Td>
          ))}
        </tr>
      ))}
    </>
  )
}
