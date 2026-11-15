import type { ComponentPropsWithRef, ReactNode } from 'react'
import { Spinner } from './Spinner'

type Variant = 'primary' | 'secondary' | 'danger' | 'ghost'
type Size = 'sm' | 'md'

const variants: Record<Variant, string> = {
  primary: 'bg-accent text-on-accent hover:bg-accent/90 border border-transparent',
  // The mockup's outlined pill: accent ink inside a 2px accent ring.
  secondary: 'bg-transparent text-accent border-2 border-accent hover:bg-accent-soft',
  danger: 'bg-transparent text-danger border-2 border-danger/60 hover:bg-danger-soft',
  ghost: 'bg-transparent text-muted border-2 border-transparent hover:text-ink hover:bg-accent-soft',
}

const sizes: Record<Size, string> = {
  sm: 'h-8 px-3.5 text-[0.82rem] gap-1.5',
  md: 'h-9 px-4.5 text-sm gap-2',
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
      className={`inline-flex cursor-pointer items-center justify-center rounded-full font-bold whitespace-nowrap transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${variants[variant]} ${sizes[size]} ${className}`}
      {...rest}
    >
      {loading && <Spinner className="size-3.5" label="Working" />}
      {children}
    </button>
  )
}
