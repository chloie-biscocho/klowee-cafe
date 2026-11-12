import type { ComponentPropsWithoutRef } from 'react'

type Variant = 'solid' | 'ghost'

const BASE = 'inline-block rounded-full px-6 py-3 text-center font-bold no-underline hover:brightness-110'
const VARIANTS: Record<Variant, string> = {
  solid: 'bg-deep text-sky',
  ghost: 'bg-transparent text-deep shadow-[inset_0_0_0_2px_var(--color-deep)]',
}

/** A pill-shaped link: deep blue, or outlined ("ghost"). */
export function LinkButton({
  variant = 'solid',
  className = '',
  ...props
}: ComponentPropsWithoutRef<'a'> & { variant?: Variant }) {
  return <a className={`${BASE} ${VARIANTS[variant]} ${className}`} {...props} />
}

/** The same pill as a real button, for actions that aren't navigation. */
export function PillButton({
  variant = 'solid',
  className = '',
  ...props
}: ComponentPropsWithoutRef<'button'> & { variant?: Variant }) {
  return <button type="button" className={`${BASE} cursor-pointer ${VARIANTS[variant]} ${className}`} {...props} />
}
