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
 * traps focus inside it, makes the page behind inert, and closes it on Escape.
 * One method shows directly; several get a segmented control at the top.
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

  return (
    <dialog
      ref={dialog}
      aria-labelledby={`${id}-title`}
      onClose={onClose}
      className="m-auto max-h-[calc(100dvh-24px)] w-[calc(100%-32px)] max-w-[380px] overflow-y-auto rounded-[22px] border-0 bg-panel px-5 py-[22px] font-sans text-ink backdrop:bg-scrim"
    >
      <form onSubmit={submit} noValidate>
        <h2 id={`${id}-title`} className="mb-1 font-serif text-[1.4rem] text-deep">
          Pay {formatPeso(value > 0 ? value : amountDue)}
        </h2>
        {note && <p className="mb-2 text-[0.95rem] text-soft">{note}</p>}

        {methods.length > 1 && (
          <div role="group" aria-label="Pay with" className="my-3 flex gap-1.5 rounded-full bg-sky p-1">
            {methods.map((m, i) => (
              <button
                key={m.id}
                type="button"
                aria-pressed={i === chosen}
                className={`flex-1 cursor-pointer rounded-full border-0 px-3 py-1.5 font-bold ${i === chosen ? 'bg-deep text-sky' : 'bg-transparent text-deep'}`}
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

        <p className="text-[0.95rem]">
          Send exactly <b>{formatPeso(value > 0 ? value : amountDue)}</b> with {method.label}.
        </p>
        {method.qrUrl && (
          <img
            src={method.qrUrl}
            alt={`${method.label} QR code for ${method.accountName}`}
            width={200}
            height={200}
            className="mx-auto my-3 block size-[min(200px,55vw)] rounded-[14px] bg-cloud object-contain"
          />
        )}
        <p className="text-[0.95rem]">
          <span className="text-soft">Account name</span> <b>{method.accountName}</b>
        </p>
        {method.accountNumber && (
          <p className="mt-1 flex flex-wrap items-center gap-2 text-[0.95rem]">
            <span className="text-soft">Number</span> <b className="tabular-nums">{method.accountNumber}</b>
            <button
              type="button"
              className="cursor-pointer rounded-full border-0 bg-sky px-3 py-1 text-[0.9rem] font-bold text-deep"
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
        {method.qrUrl && (
          <p className="mt-3 text-[0.9rem] text-soft">
            <a
              href={method.qrUrl}
              download={`klowee-${method.label.toLowerCase().replace(/\W+/g, '-')}-qr`}
              className="font-bold text-deep underline"
              onClick={(event) => saveQr(event, method.qrUrl!, `klowee-${method.label.toLowerCase().replace(/\W+/g, '-')}-qr`)}
            >
              Save QR
            </a>{' '}
            On this phone? Save the QR, then choose Upload QR in your banking app, or copy the number.
          </p>
        )}

        <label className="mt-4 block text-[0.93rem] font-medium" htmlFor={`${id}-amount`}>
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
      </form>
    </dialog>
  )
}
