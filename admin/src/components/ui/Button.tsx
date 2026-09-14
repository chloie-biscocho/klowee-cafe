import type { ComponentPropsWithRef, ReactNode } from 'react'
import { Spinner } from './Spinner'

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost'
type Size = 'sm' | 'md'

const variants: Record<Variant, string> = {
  primary: 'bg-accent text-white hover:bg-accent/90 border border-transparent',
  secondary: 'bg-surface text-ink border border-line hover:bg-canvas',
  danger: 'bg-surface text-danger border border-danger/30 hover:bg-danger-soft',
  ghost: 'bg-transparent text-muted border border-transparent hover:text-ink hover:bg-canvas',
}

const sizes: Record<Size, string> = {
  sm: 'h-8 px-2.5 text-xs gap-1.5',
  md: 'h-9 px-3.5 text-sm gap-2',
}

interface ButtonProps extends ComponentPropsWithRef<'button'> {
  variant?: Variant
  size?: Size
  /** Shows a spinner and blocks further clicks while a mutation is in flight. */
  loading?: boolean
  children?: ReactNode
}

export function Button({
  variant = 'secondary',
  size = 'md',
  loading = false,
  disabled,
  className = '',
  children,
  type = 'button',
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      disabled={disabled || loading}
      className={`inline-flex items-center justify-center rounded-md font-medium whitespace-nowrap transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${variants[variant]} ${sizes[size]} ${className}`}
      {...rest}
    >
      {loading && <Spinner className="size-3.5" label="Working" />}
      {children}
    </button>
  )
}
