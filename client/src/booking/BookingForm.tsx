import { useEffect, useId, useRef, useState, type ChangeEvent, type FormEvent, type ReactNode } from 'react'
import type { PublicPackage } from '../api/types'
import { PillButton } from '../components/Button'
import {
  businessToday,
  EMPTY_BOOKING,
  FIELD_ORDER,
  latestEventDate,
  LIMITS,
  sendBooking,
  validateBooking,
  type BookingErrors,
  type BookingField,
  type BookingValues,
  type PackageChoice,
} from '../lib/booking'

const FIELD = 'mt-1 block w-full rounded-xl border-[1.5px] bg-cloud px-3 py-2.5 text-ink placeholder:text-soft'
const LABEL = 'block text-[0.93rem] font-medium'

/** Grid widths: one column below 520px, two from 520px, six from 820px. */
const HALF = 'min-[820px]:col-span-3'
const THIRD = 'min-[820px]:col-span-2'
const FULL = 'min-[520px]:col-span-2 min-[820px]:col-span-6'

/**
 * The booking request form (handover 20), its own lazy chunk. Errors show
 * under each field on submit, with focus on the first; the API's own field
 * errors land the same way. Anything else (too many tries, no network, the
 * form switched off meanwhile) is one message above the button, and what was
 * typed stays. On success the form gives way to a confirmation that takes focus.
 */
export default function BookingForm({ packages, choice, reserve = '' }: { packages: PublicPackage[]; choice: PackageChoice; reserve?: string }) {
  const id = useId()
  const [values, setValues] = useState<BookingValues>(() => ({ ...EMPTY_BOOKING, packageId: choice.packageId ?? '' }))
  const [errors, setErrors] = useState<BookingErrors>({})
  const [message, setMessage] = useState<string | null>(null)
  const [sending, setSending] = useState(false)
  const [sent, setSent] = useState(false)
  const [seenChoice, setSeenChoice] = useState(choice.nonce)
  const fields = useRef<Partial<Record<BookingField, HTMLElement | null>>>({})
  const confirmation = useRef<HTMLDivElement>(null)
  const today = businessToday()

  // A package card pressed after the form appeared: select it (and bring the form back if it was sent).
  if (choice.nonce !== seenChoice) {
    setSeenChoice(choice.nonce)
    setValues((current) => ({ ...current, packageId: choice.packageId ?? '' }))
    setSent(false)
  }

  useEffect(() => {
    if (sent) confirmation.current?.focus()
  }, [sent])

  const set = (field: keyof BookingValues) => (event: ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
    setValues((current) => ({ ...current, [field]: event.target.value }))

  const focusFirst = (problems: BookingErrors) => {
    const first = FIELD_ORDER.find((field) => problems[field])
    if (first) fields.current[first]?.focus()
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (sending) return
    const problems = validateBooking(values, today)
    setErrors(problems)
    setMessage(null)
    if (Object.keys(problems).length > 0) {
      focusFirst(problems)
      return
    }

    setSending(true)
    const result = await sendBooking(values)
    setSending(false)
    if (result.ok) {
      setSent(true)
      return
    }
    setErrors(result.fields)
    setMessage(result.message)
    focusFirst(result.fields)
  }

  const another = () => {
    setValues(EMPTY_BOOKING)
    setErrors({})
    setMessage(null)
    setSent(false)
  }

  if (sent) {
    return (
      <div ref={confirmation} tabIndex={-1} className="rounded-2xl bg-sky/60 px-5 py-8 text-center focus:outline-none focus-visible:outline-3">
        <p className="font-serif text-[1.4rem] text-deep">Request sent. Klowee will get back to you.</p>
        <PillButton variant="ghost" className="mt-5" onClick={another}>
          Send another
        </PillButton>
      </div>
    )
  }

  const describe = (field: BookingField, hint?: string) =>
    [hint && `${id}-${field}-hint`, errors[field] && `${id}-${field}-error`].filter(Boolean).join(' ') || undefined
  const props = (field: BookingField, hint?: string, extra = '') => ({
    id: `${id}-${field}`,
    ref: (element: HTMLElement | null) => {
      fields.current[field] = element
    },
    value: values[field],
    onChange: set(field),
    'aria-invalid': errors[field] ? true : undefined,
    'aria-describedby': describe(field, hint),
    className: `${FIELD} ${errors[field] ? 'border-deep border-2' : 'border-line'} ${extra}`,
  })

  return (
    // Layout B (handover 20a, owner's choice): six columns from 820px (name and contact a half each;
    // date, guests and package a third each, or date and guests a half each without packages; venue
    // and message full width), two columns from 520px, one below.
    <form noValidate onSubmit={submit} aria-label="Booking request" className={`relative grid content-start gap-4 text-left min-[520px]:grid-cols-2 min-[820px]:grid-cols-6 ${reserve}`}>
      <Field label="Your name" field="name" id={id} error={errors.name} span={HALF}>
        <input {...props('name')} autoComplete="name" maxLength={LIMITS.name} required />
      </Field>
      <Field label="How to reach you" field="contact" id={id} error={errors.contact} hint="Mobile number, Messenger or Instagram" span={HALF}>
        <input {...props('contact', 'hint')} autoComplete="tel" maxLength={LIMITS.contact} required />
      </Field>
      <Field label="Event date" field="eventDate" id={id} error={errors.eventDate} span={packages.length > 0 ? THIRD : HALF}>
        <input {...props('eventDate')} type="date" min={today} max={latestEventDate(today)} required />
      </Field>
      <Field label="Number of guests (optional)" field="guestCount" id={id} error={errors.guestCount} span={packages.length > 0 ? THIRD : HALF}>
        <input {...props('guestCount')} type="number" inputMode="numeric" min={LIMITS.minGuests} max={LIMITS.maxGuests} step={1} />
      </Field>
      {packages.length > 0 && (
        <Field label="Package" field="packageId" id={id} error={errors.packageId} span={THIRD}>
          <select {...props('packageId')}>
            <option value="">Not sure yet</option>
            {packages.map((pkg) => (
              <option key={pkg.id} value={pkg.id}>
                {pkg.name}
              </option>
            ))}
          </select>
        </Field>
      )}
      <Field label="Venue" field="venue" id={id} error={errors.venue} span={FULL}>
        <input {...props('venue')} maxLength={LIMITS.venue} required />
      </Field>
      <Field
        label="Anything else (optional)"
        field="message"
        id={id}
        error={errors.message}
        span={FULL}
        hint={`${values.message.length} / ${LIMITS.message}`}
        counter
      >
        <textarea {...props('message', 'hint', 'resize-y')} rows={3} maxLength={LIMITS.message} />
      </Field>

      {/* The honeypot: off screen, out of the tab order, hidden from screen readers, never autofilled. */}
      <div aria-hidden="true" className="absolute -left-[10000px] h-px w-px overflow-hidden">
        <label htmlFor={`${id}-website`}>Website</label>
        <input id={`${id}-website`} name="website" tabIndex={-1} autoComplete="off" value={values.website} onChange={set('website')} />
      </div>

      <div className={FULL}>
        {message && (
          <p role="alert" className="mb-3 text-[0.95rem] font-bold text-deep">
            {message}
          </p>
        )}
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
          <PillButton type="submit" className="w-full disabled:cursor-default disabled:opacity-60 min-[520px]:w-auto" disabled={sending}>
            {sending ? 'Sending your request…' : 'Send booking request'}
          </PillButton>
          <p className="text-[0.9rem] text-soft">Your details are only used to reply about this booking.</p>
        </div>
      </div>
    </form>
  )
}

function Field({
  label,
  field,
  id,
  error,
  hint,
  counter,
  span,
  children,
}: {
  label: string
  field: BookingField
  id: string
  error?: string
  hint?: string
  counter?: boolean
  /** Its width in the grid (HALF, THIRD or FULL). */
  span: string
  children: ReactNode
}) {
  // Under the input, so side-by-side inputs stay level; a counter sits at the right.
  const hintLine = hint && (
    <span id={`${id}-${field}-hint`} className={`mt-1 block text-[0.85rem] text-soft ${counter ? 'text-right' : ''}`}>
      {hint}
    </span>
  )
  return (
    <div className={span}>
      <label htmlFor={`${id}-${field}`} className={LABEL}>
        {label}
      </label>
      {children}
      {hintLine}
      {error && (
        <p id={`${id}-${field}-error`} className="mt-1 text-[0.9rem] font-bold text-deep">
          {error}
        </p>
      )}
    </div>
  )
}
