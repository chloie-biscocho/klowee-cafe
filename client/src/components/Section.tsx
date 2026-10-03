import type { ReactNode } from 'react'

type Band = 'paper' | 'sky'

/**
 * A page section: a full-width band (paper or sky) with the 1120px container
 * inside. Every section is labelled by a heading; where the brief gives none,
 * `heading` is visually hidden and reuses the nav label.
 */
export function Section({
  id,
  band,
  heading,
  hideHeading = false,
  children,
  decoration,
}: {
  id: string
  band: Band
  heading: ReactNode
  hideHeading?: boolean
  children: ReactNode
  decoration?: ReactNode
}) {
  const headingId = `${id}-heading`
  return (
    <section
      id={id}
      aria-labelledby={headingId}
      className={`relative py-16 md:py-24 ${band === 'sky' ? 'bg-sky' : 'bg-paper'}`}
    >
      {decoration}
      <div className="relative mx-auto max-w-page px-5 md:px-8">
        <h2
          id={headingId}
          className={
            hideHeading
              ? 'sr-only'
              : 'mb-8 font-heading text-4xl leading-[1.05] text-navy md:text-5xl'
          }
        >
          {heading}
        </h2>
        {children}
      </div>
    </section>
  )
}
