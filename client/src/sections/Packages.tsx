import type { PublicHome } from '../api/types'
import { LinkButton } from '../components/Button'
import { FormattedText } from '../components/FormattedText'
import { bookingFormEnabled } from '../lib/booking'
import { formatPeso } from '../lib/format'

/**
 * One white card per active package. No packages, no section. With the
 * booking form on, "Book this" scrolls to the form with that package chosen;
 * with it off, to the Book section as before.
 */
export function Packages({ home, onBook }: { home: PublicHome; onBook?: (packageId: string) => void }) {
  if (home.packages.length === 0) return null
  const formOn = bookingFormEnabled(home)

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
            {/* The owners' description, formatted (handover 21); none, no space. */}
            <FormattedText text={pkg.description} className="mb-3 text-[0.95rem]" paragraphClassName="mt-2 first-of-type:mt-0" listClassName="mt-2" />
            <p className="mb-3 text-[1.8rem] font-bold tracking-[-0.01em]">{formatPeso(pkg.price)}</p>
            <ul className="mb-5 flex-1">
              {pkg.inclusions.map((inclusion, index) => (
                <li key={index} className="border-t-[1.5px] border-line py-1.5">
                  {inclusion}
                </li>
              ))}
            </ul>
            <LinkButton
              href={formOn ? '#booking-form' : '#book'}
              variant="ghost"
              aria-label={`Book this: ${pkg.name}`}
              onClick={formOn ? () => onBook?.(pkg.id) : undefined}
            >
              Book this
            </LinkButton>
          </li>
        ))}
      </ul>
    </section>
  )
}
