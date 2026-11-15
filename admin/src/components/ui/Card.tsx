import type { ReactNode } from 'react'

export function Card({ className = '', children }: { className?: string; children: ReactNode }) {
  return (
    <div className={`rounded-[18px] bg-surface ${className}`}>{children}</div>
  )
}

export function CardHeader({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 border-b-[1.5px] border-line px-5 py-3.5">
      {children}
    </div>
  )
}
