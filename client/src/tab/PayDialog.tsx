import { useEffect, useId, useRef, useState, type FormEvent, type MouseEvent } from 'react'
import { PillButton } from '../components/Button'
import { formatPeso } from '../lib/format'
import type { ClaimRequest, TabPayMethod } from './types'
import type { WriteResult } from './useTab'

const REFERENCE = /^[A-Za-z0-9]{6,32}$/

/**
 * Downloads the QR image so a customer on this same phone can pick it in
 * their banking app ("Upload QR"). The image lives on another origin, where
 * the `download` attribute is ignored, so it is fetched as a blob first; if
 * that fails, it opens in a new tab to save from there.
 */
async function saveQr(event: MouseEvent<HTMLAnchorElement>, url: string, name: string) {
  event.preventDefault()
  try {
    const response = await fetch(url)
    if (!response.ok) throw new Error(`HTTP ${response.status}`)
    const href = URL.createObjectURL(await response.blob())
    const link = document.createElement('a')
    link.href = href
    link.download = name
    link.click()
    URL.revokeObjectURL(href)
  } catch {
    window.open(url, '_blank', 'noopener')
  }
}

/**
 * The Pay sheet: a native <dialog> opened with showModal(), so the browser
 * traps focus inside it, makes the page behind inert, closes it on Escape and
 * returns focus to Pay. One method shows directly; several get a segmented
 * control. On a phone it is a sheet at the foot of the screen; from 760px a
 * roomy two-column dialog with the QR large on the left (handover 17b).
 */
export function PayDialog({
  open,
  onClose,
  methods,
  note,
  amountDue,
  onSubmit,
}: {
  open: boolean
  onClose: () => void
  methods: TabPayMethod[]
  note: string | null
  amountDue: number
  onSubmit: (claim: ClaimRequest) => Promise<WriteResult>
}) {
  const id = useId()
  const dialog = useRef<HTMLDialogElement>(null)
  const [chosen, setChosen] = useState(0)
  const [amount, setAmount] = useState(String(amountDue))
  const [reference, setReference] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const [sending, setSending] = useState(false)
  const method = methods[Math.min(chosen, methods.length - 1)]

  useEffect(() => {
    const element = dialog.current
    if (!element) return
    if (open && !element.open) {
      // A fresh sheet each time: the whole balance, no old reference.
      setAmount(String(amountDue))
      setReference('')
      setError(null)
      setCopied(false)
      element.showModal()
    } else if (!open && element.open) {
      element.close()
    }
  }, [open, amountDue])

  if (!method) return null

  const value = Number(amount)
  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const cleaned = reference.replace(/\s+/g, '')
    if (!Number.isFinite(value) || value <= 0) return setError('Enter the amount you sent.')
    if (value > amountDue) return setError(`That's more than your tab: you can report up to ${formatPeso(amountDue)}.`)
    if (!REFERENCE.test(cleaned)) return setError('The reference number is 6 to 32 letters and digits, as your app shows it.')
    setSending(true)
    setError(null)
    const result = await onSubmit({ amount: value, reference: cleaned, paymentMethodId: method.id })
    setSending(false)
    if (result.ok) onClose()
    else setError(result.message)
  }

  const hasQr = Boolean(method.qrUrl)
  const qrName = `klowee-${method.label.toLowerCase().replace(/\W+/g, '-')}-qr`
  const shown = formatPeso(value > 0 ? value : amountDue)

  // Phone: one column, in this order (CSS order, so the focusable parts keep
  // their relative order): switch, amount, QR, Save QR, account, note, form.
  // From md (760px): the QR column on the left, everything else on the right;
  // the two wrappers stop being `contents` and become columns.
  const column = 'contents md:flex md:min-w-0 md:flex-col md:gap-3.5'

  return (
    <dialog
      ref={dialog}
      aria-labelledby={`${id}-title`}
      onClose={onClose}
      className={`pay-sheet mt-auto mb-0 max-h-[92dvh] w-[calc(100%-24px)] max-w-none overflow-hidden rounded-t-[22px] rounded-b-none border-0 bg-panel p-0 font-sans text-ink backdrop:bg-scrim open:flex open:flex-col md:m-auto md:max-h-[calc(100dvh-48px)] md:rounded-[22px] ${
        hasQr ? 'md:w-[min(760px,calc(100%-48px))]' : 'md:w-[min(520px,calc(100%-48px))]'
      }`}
    >
      {/* The header stays put while the body scrolls (a phone keyboard shrinks the sheet). */}
      <div className="flex shrink-0 items-center justify-between gap-3 border-b border-line px-5 py-3 md:px-7">
        <h2 id={`${id}-title`} className="font-serif text-[1.4rem] text-deep">
          Pay {shown}
        </h2>
        <button
          type="button"
          aria-label="Close"
          className="grid size-9 flex-none cursor-pointer place-content-center rounded-full border-0 bg-sky text-[1.1rem] font-bold text-deep"
          onClick={onClose}
        >
          <span aria-hidden="true">✕</span>
        </button>
      </div>

      <form onSubmit={submit} noValidate className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-5 pt-4 pb-5 md:px-7 md:pt-5 md:pb-6">
        <div className={`flex flex-col gap-3.5 ${hasQr ? 'md:grid md:grid-cols-[minmax(0,332px)_minmax(0,1fr)] md:items-start md:gap-x-7' : ''}`}>
          <div className={column}>
            {methods.length > 1 && (
              <div role="group" aria-label="Pay with" className="order-1 flex gap-1.5 rounded-full bg-sky p-1 md:order-none">
                {methods.map((m, i) => (
                  <button
                    key={m.id}
                    type="button"
                    aria-pressed={i === chosen}
                    className={`min-w-0 flex-1 cursor-pointer rounded-full border-0 px-3 py-1.5 leading-tight font-bold break-words ${i === chosen ? 'bg-deep text-sky' : 'bg-transparent text-deep'}`}
                    onClick={() => {
                      setChosen(i)
                      setCopied(false)
                    }}
                  >
                    {m.label}
                  </button>
                ))}
              </div>
            )}

            {hasQr && (
              <>
                {/* A fixed square box on a white card with a quiet border: the
                    code scans against either theme, takes its space before it
                    loads, and is never stretched past its natural size. */}
                <div className="order-3 flex justify-center md:order-none">
                  <div className="rounded-[18px] bg-card-slot p-4 shadow-[0_0_0_1px_var(--color-line)]">
                    <div data-qr-box className="flex size-[min(320px,calc(100vw-96px))] items-center justify-center md:size-[300px]">
                      <img
                        src={method.qrUrl!}
                        alt={`${method.label} QR code for ${method.accountName}`}
                        className="block h-auto max-h-full w-auto max-w-full"
                      />
                    </div>
                  </div>
                </div>
                <p className="order-4 text-center text-[0.9rem] text-soft md:order-none">
                  <a href={method.qrUrl!} download={qrName} className="font-bold text-deep underline" onClick={(event) => saveQr(event, method.qrUrl!, qrName)}>
                    Save QR
                  </a>{' '}
                  On this phone? Save the QR, then choose Upload QR in your banking app, or copy the number.
                </p>
              </>
            )}
          </div>

          <div className={column}>
            <p className="order-2 text-[0.95rem] md:order-none">
              Send exactly <b className="font-serif text-[1.9rem] leading-tight text-deep md:block md:text-[2.4rem]">{shown}</b>{' '}
              with {method.label}.
            </p>

            <div className="order-5 rounded-2xl bg-sky/60 px-4 py-3 text-[0.95rem] md:order-none">
              <p className="min-w-0 break-words">
                <span className="text-soft">Account name</span> <b>{method.accountName}</b>
              </p>
              {method.accountNumber && (
                <p className="mt-1 flex flex-wrap items-center gap-2">
                  <span className="text-soft">Number</span> <b className="tabular-nums">{method.accountNumber}</b>
                  <button
                    type="button"
                    className="cursor-pointer rounded-full border-0 bg-deep px-3 py-1 text-[0.9rem] font-bold text-sky"
                    onClick={async () => {
                      try {
                        await navigator.clipboard.writeText(method.accountNumber!)
                        setCopied(true)
                      } catch {
                        setCopied(false)
                      }
                    }}
                  >
                    Copy
                  </button>
                  <span aria-live="polite" className="text-[0.9rem] text-soft">
                    {copied ? 'Copied' : ''}
                  </span>
                </p>
              )}
            </div>

            {note && <p className="order-6 text-[0.95rem] text-soft md:order-none">{note}</p>}

            <div className="order-7 md:order-none">
              <label className="block text-[0.93rem] font-medium" htmlFor={`${id}-amount`}>
                Amount sent
              </label>
              <input
                id={`${id}-amount`}
                type="number"
                inputMode="decimal"
                min={0.01}
                max={amountDue}
                step="0.01"
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
                className="mt-1 block w-full rounded-xl border-[1.5px] border-line bg-cloud px-3 py-2.5 text-ink"
              />
              <label className="mt-3 block text-[0.93rem] font-medium" htmlFor={`${id}-ref`}>
                {method.label} reference number
              </label>
              <input
                id={`${id}-ref`}
                autoComplete="off"
                autoCapitalize="characters"
                spellCheck={false}
                placeholder="From your receipt in the app"
                value={reference}
                aria-describedby={error ? `${id}-error` : undefined}
                onChange={(event) => setReference(event.target.value)}
                className="mt-1 block w-full rounded-xl border-[1.5px] border-line bg-cloud px-3 py-2.5 text-ink placeholder:text-soft"
              />
              {error && (
                <p id={`${id}-error`} role="alert" className="mt-2 text-[0.93rem] font-bold text-deep">
                  {error}
                </p>
              )}
              <div className="mt-4 flex gap-2.5">
                <PillButton variant="ghost" className="flex-1 px-3" onClick={onClose}>
                  Not yet
                </PillButton>
                <PillButton type="submit" className="flex-1 px-3" disabled={sending}>
                  {sending ? 'Sending…' : "I've paid"}
                </PillButton>
              </div>
            </div>
          </div>
        </div>
      </form>
    </dialog>
  )
}
