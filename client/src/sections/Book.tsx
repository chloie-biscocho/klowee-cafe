import { lazy, Suspense } from 'react'
import type { PublicHome } from '../api/types'
import { LinkButton } from '../components/Button'
import { bookingFormEnabled, NO_PACKAGE_CHOICE, type PackageChoice } from '../lib/booking'
import { setting } from '../lib/content'

// Its own chunk, so the form never delays the first paint (handover 20). It
// starts loading as soon as the page's data arrives, not on scroll.
const BookingForm = lazy(() => import('../booking/BookingForm'))

/**
 * The form's space while its chunk loads, so nothing below moves when it
 * arrives: the form's tallest height in each layout, measured in the browser
 * (862px at a 320px phone, 636px from 760px; the package field is 87px of
 * that with its gap). Errors shown after a submit may grow it, which is the
 * visitor's own doing, not a shift.
 */
const RESERVED = {
  withPackages: 'min-h-[862px] md:min-h-[636px]',
  withoutPackages: 'min-h-[775px] md:min-h-[549px]',
}

/**
 * The centred white panel. With the booking form on (the default), the
 * request form comes first and the contact buttons follow, "or message us".
 * With it switched off, exactly the section as it was: the line from
 * Settings and the buttons whose settings are filled in.
 */
export function Book({ home, choice = NO_PACKAGE_CHOICE }: { home: PublicHome; choice?: PackageChoice }) {
  const body = setting(home, 'book_body')
  const note = setting(home, 'booking_form_note')
  const email = setting(home, 'contact_email')
  const instagram = setting(home, 'instagram_url')
  const facebook = setting(home, 'facebook_url')
  const external = { target: '_blank', rel: 'noopener noreferrer' }
  const formOn = bookingFormEnabled(home)
  const hasButtons = Boolean(email || instagram || facebook)

  return (
    <section
      id="book"
      aria-labelledby="book-heading"
      className="mb-[26px] rounded-[22px] bg-panel px-5 py-[26px] text-center md:rounded-[28px] md:px-7 md:pt-12 md:pb-[46px]"
    >
      <h2 id="book-heading" className="mb-1.5 font-serif text-[clamp(1.7rem,4vw,2.3rem)] leading-[1.1] text-deep">
        Bring the cart to your event
      </h2>
      {body && <p className="mx-auto mb-6 max-w-[56ch] text-soft">{body}</p>}
      {formOn && (
        <>
          {note && <p className="mx-auto mb-5 max-w-[56ch] text-[0.95rem] font-medium text-deep">{note}</p>}
          <div id="booking-form" className={`flex scroll-mt-4 flex-col justify-center ${home.packages.length > 0 ? RESERVED.withPackages : RESERVED.withoutPackages}`}>
            <Suspense fallback={null}>
              <BookingForm packages={home.packages} choice={choice} />
            </Suspense>
          </div>
          {hasButtons && <p className="mt-8 mb-3 text-soft">or message us</p>}
        </>
      )}
      {hasButtons && (
        <div className="flex flex-wrap justify-center gap-3">
          {email && <LinkButton href={`mailto:${email}`}>Email us</LinkButton>}
          {instagram && (
            <LinkButton href={instagram} variant="ghost" {...external}>
              Message on Instagram
            </LinkButton>
          )}
          {facebook && (
            <LinkButton href={facebook} variant="ghost" {...external}>
              Facebook
            </LinkButton>
          )}
        </div>
      )}
    </section>
  )
}
