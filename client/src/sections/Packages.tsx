import type { PublicHome } from '../api/types'
import { PaperCard } from '../components/PaperCard'
import { Pill } from '../components/Pill'
import { Section } from '../components/Section'
import { formatPeso } from '../lib/format'

/** Event packages on a sky band. No packages, no section — not even its heading. */
export function Packages({ home }: { home: PublicHome }) {
  if (home.packages.length === 0) return null

  return (
    <Section id="packages" band="sky" heading="packages" hideHeading>
      <ul className="grid gap-6 md:grid-cols-3">
        {home.packages.map((pkg) => (
          <li key={pkg.id}>
            <PaperCard as="article" className="flex h-full flex-col px-6 py-7">
              <h3 className="font-heading text-3xl leading-[1.05] text-navy">{pkg.name}</h3>
              <p className="mt-2 text-2xl font-bold text-cobalt">{formatPeso(pkg.price)}</p>
              {pkg.guestCountNote && (
                <p className="font-hand text-2xl font-medium text-ink-muted">{pkg.guestCountNote}</p>
              )}
              {pkg.description && <p className="mt-3 text-navy">{pkg.description}</p>}
              {pkg.inclusions.length > 0 && (
                <ul className="mt-4 flex flex-col gap-2">
                  {pkg.inclusions.map((inclusion, index) => (
                    <li key={index} className="flex items-start gap-3 text-navy">
                      <span aria-hidden="true" className="mt-2.5 size-2 shrink-0 rounded-full bg-cobalt" />
                      {inclusion}
                    </li>
                  ))}
                </ul>
              )}
              <div className="mt-auto pt-6">
                <Pill href="#book" aria-label={`Book this: ${pkg.name}`}>
                  Book this
                </Pill>
              </div>
            </PaperCard>
          </li>
        ))}
      </ul>
    </Section>
  )
}
