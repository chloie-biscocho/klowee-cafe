import { lazy, Suspense, type ReactNode } from 'react'
import type { PublicHome } from '../api/types'
import { LinkButton } from '../components/Button'
import { bookingFormEnabled, bookingSteps, NO_PACKAGE_CHOICE, type PackageChoice } from '../lib/booking'
import { setting } from '../lib/content'

// Its own chunk, so the form never delays the first paint (handover 20). It
// starts loading as soon as the page's data arrives, not on scroll.
const BookingForm = lazy(() => import('../booking/BookingForm'))

/**
 * The form's space while its chunk loads, so nothing below moves when it
 * arrives: the form's tallest height in each layout B arrangement, measured in
 * the browser (handover 20a report): 862px with one field per row below 520px
 * (at a 320px phone), 657px with two columns from 520px, 518px with six
 * columns from 820px. Without packages the package field's row goes below
 * 820px (87px); from 820px it shares a row, so nothing changes. Errors shown
 * after a submit may grow it, which is the visitor's own doing, not a shift.
 */
const RESERVED = {
  withPackages: 'min-h-[862px] min-[520px]:min-h-[657px] min-[820px]:min-h-[518px]',
  withoutPackages: 'min-h-[775px] min-[520px]:min-h-[570px] min-[820px]:min-h-[518px]',
}

/**
 * The Book panel. With the booking form on (the default), layout B of
 * `docs/mockups/klowee-booking-layout-sample.html` (handover 20a, the owner's
 * final choice): a top row with the words on the left (heading, line, note,
 * the numbered steps) and "or message us" with the contact buttons on the
 * right (wrapping under them when narrow), then the form across the panel.
 * With the form switched off, exactly the section as it was before handover
 * 20: centred heading, line and buttons.
 */
export function Book({ home, choice = NO_PACKAGE_CHOICE }: { home: PublicHome; choice?: PackageChoice }) {
  const body = setting(home, 'book_body')
  const email = setting(home, 'contact_email')
  const instagram = setting(home, 'instagram_url')
  const facebook = setting(home, 'facebook_url')
  const external = { target: '_blank', rel: 'noopener noreferrer' }
  const hasButtons = Boolean(email || instagram || facebook)

  const buttons = (className: string): ReactNode =>
    hasButtons && (
      <div className={`flex flex-wrap gap-3 ${className}`}>
        {email && <LinkButton href={`mailto:${email}`}>Email us</LinkButton>}
        {instagram && (
          <LinkButton href={instagram} variant="ghost" {...external}>
            Instagram
          </LinkButton>
        )}
        {facebook && (
          <LinkButton href={facebook} variant="ghost" {...external}>
            Facebook
          </LinkButton>
        )}
      </div>
    )

  if (!bookingFormEnabled(home)) {
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
        {buttons('justify-center')}
      </section>
    )
  }

  const note = setting(home, 'booking_form_note')
  const steps = bookingSteps(home)
  const reserve = home.packages.length > 0 ? RESERVED.withPackages : RESERVED.withoutPackages

  return (
    <section
      id="book"
      aria-labelledby="book-heading"
      className="mb-[26px] rounded-[22px] bg-panel px-5 py-[26px] md:rounded-[28px] md:px-10 md:py-11"
    >
      <div className="mb-7 flex flex-wrap items-end justify-between gap-x-6 gap-y-5">
        <div className="min-w-0 flex-[1_1_20rem]">
          <h2 id="book-heading" className="font-serif text-[clamp(1.7rem,4vw,2.3rem)] leading-[1.1] text-balance text-deep">
            Bring the cart to your event
          </h2>
          {body && <p className="mt-2.5 max-w-[46ch] text-soft">{body}</p>}
          {note && <p className="mt-3 max-w-[46ch] text-[0.95rem] font-medium text-deep">{note}</p>}
          {steps.length > 0 && (
            // An ordered list: the order is the meaning. role="list" keeps it a list in Safari, which drops it with list-style none.
            <ol role="list" aria-label="How booking works" className="mt-5 flex list-none flex-col gap-3 p-0">
              {steps.map((step, index) => (
                <li key={index} className="flex items-start gap-3">
                  <span className="grid size-[1.9rem] flex-none place-items-center rounded-full bg-sky/60 font-serif text-deep">{index + 1}</span>
                  <span className="pt-0.5">{step}</span>
                </li>
              ))}
            </ol>
          )}
        </div>
        {hasButtons && (
          <div data-part="chat" className="flex-none">
            <p className="mb-2.5 text-[0.95rem] font-bold text-soft">or message us</p>
            {buttons('')}
          </div>
        )}
      </div>

      {/* The reserve sits on the loading placeholder and on the form itself, not on this box:
          nothing moves when the form arrives, and the short "sent" confirmation takes only its own space. */}
      <div id="booking-form" className="scroll-mt-4">
        <Suspense fallback={<div aria-hidden="true" className={reserve} />}>
          <BookingForm packages={home.packages} choice={choice} reserve={reserve} />
        </Suspense>
      </div>
    </section>
  )
}
