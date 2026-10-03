import type { ComponentPropsWithoutRef } from 'react'

type Tone = 'cobalt' | 'pink'

// Hover changes colour only, never position (the brief's motion rule).
const tones: Record<Tone, string> = {
  cobalt: 'bg-cobalt text-paper hover:bg-navy',
  pink: 'bg-pink text-navy hover:bg-sky-light',
}

const base =
  'inline-flex min-h-11 items-center justify-center rounded-full px-5 py-2 font-body text-base font-bold whitespace-nowrap'

/** The site's buttons are all links: to a section, to email, or out to social. */
export function Pill({
  tone = 'cobalt',
  className = '',
  ...rest
}: ComponentPropsWithoutRef<'a'> & { tone?: Tone }) {
  return <a className={`${base} ${tones[tone]} ${className}`} {...rest} />
}

/** The same look for the few real buttons (retry). */
export function PillButton({
  tone = 'cobalt',
  className = '',
  type = 'button',
  ...rest
}: ComponentPropsWithoutRef<'button'> & { tone?: Tone }) {
  return <button type={type} className={`${base} ${tones[tone]} ${className}`} {...rest} />
}
