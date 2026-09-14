import type { ReactNode } from 'react'

/**
 * The label / control / error wrapper shared by Input, Select and Textarea, so
 * the three stay identical in spacing and in how they announce an error.
 */
export function Field({
  id,
  label,
  error,
  hint,
  children,
}: {
  id: string
  label: string
  error?: string
  hint?: string
  children: ReactNode
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-xs font-semibold text-muted">
        {label}
      </label>
      {children}
      {hint && !error && <p className="text-xs text-muted">{hint}</p>}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs text-danger">
          {error}
        </p>
      )}
    </div>
  )
}

export const controlClasses =
  'h-9 w-full rounded-md border border-line bg-surface px-2.5 text-sm text-ink placeholder:text-muted/70 disabled:bg-canvas disabled:text-muted'
