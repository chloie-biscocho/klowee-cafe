import type { PublicHome } from '../api/types'
import { LinkButton } from '../components/Button'
import { formatPeso } from '../lib/format'

/** One white card per active package. No packages, no section. */
export function Packages({ home }: { home: PublicHome }) {
  if (home.packages.length === 0) return null

  return (
    <section id="packages" aria-labelledby="packages-heading" className="pt-5 pb-[30px]">
      <h2 id="packages-heading" className="mb-[22px] font-serif text-[clamp(1.7rem,4vw,2.3rem)] leading-[1.1] text-deep">
        Event packages
      </h2>
      <ul className="mb-2 grid gap-4 md:grid-cols-3">
        {home.packages.map((pkg) => (
          <li key={pkg.id} className="flex flex-col rounded-3xl bg-panel px-6 pt-[26px] pb-6">
            <h3 className="font-serif text-[1.4rem] text-deep">{pkg.name}</h3>
            {pkg.guestCountNote && <p className="mb-3 text-soft">{pkg.guestCountNote}</p>}
            <p className="mb-3 text-[1.8rem] font-bold tracking-[-0.01em]">{formatPeso(pkg.price)}</p>
            <ul className="mb-5 flex-1">
              {pkg.inclusions.map((inclusion, index) => (
                <li key={index} className="border-t-[1.5px] border-line py-1.5">
                  {inclusion}
                </li>
              ))}
            </ul>
            <LinkButton href="#book" variant="ghost" aria-label={`Book this: ${pkg.name}`}>
              Book this
            </LinkButton>
          </li>
        ))}
      </ul>
    </section>
  )
}
