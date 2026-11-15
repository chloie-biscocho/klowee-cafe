import { useEffect, useId, useRef, useState, type ChangeEvent, type ComponentPropsWithRef, type KeyboardEvent, type ReactNode } from 'react'
import { isSafeUrl, type Align } from '../../lib/formatted'
import { controlClasses } from './Field'
import { alignmentAt, applyEdit, selectionIsLink, setAlignment, toggleLink, toggleList, toggleWrap, type Edit } from './formatEdits'
import { FormattedPreview } from './FormattedPreview'

const PREVIEW_KEY = 'klowee.admin.formatPreview'

function readPreviewOpen(): boolean {
  try {
    return localStorage.getItem(PREVIEW_KEY) !== 'closed'
  } catch {
    return true
  }
}

interface FormattedTextareaProps extends Omit<ComponentPropsWithRef<'textarea'>, 'id'> {
  label: string
  error?: string
  hint?: string
}

/**
 * A text area for the owners' longer texts (handover 21): a toolbar for
 * bold, italic, links, lists and paragraph alignment, writing the small
 * Markdown subset of `lib/formatted.ts`, and a live preview drawn by the
 * site's own rules. The box stays an ordinary text area (keyboard, screen
 * readers, phones), and works with `register()` or as a controlled field.
 *
 * Every toolbar action is one browser edit (`document.execCommand('insertText')`
 * on the range it changes), so it fires a normal input event and Ctrl+Z undoes
 * it. Where the browser refuses that command, `setRangeText` and an input
 * event do the same edit (without the undo step).
 */
export function FormattedTextarea({ label, error, hint, className = '', rows = 5, ref, onChange, onKeyDown, onSelect, value, defaultValue, ...rest }: FormattedTextareaProps) {
  const id = useId()
  const area = useRef<HTMLTextAreaElement | null>(null)
  const [text, setText] = useState(String(value ?? defaultValue ?? ''))
  const [cursor, setCursor] = useState(0)
  const [link, setLink] = useState<{ start: number; end: number } | null>(null)
  const [url, setUrl] = useState('')
  const [linkError, setLinkError] = useState<string | null>(null)
  const [previewOpen, setPreviewOpen] = useState(readPreviewOpen)

  // register() fills the box through its ref after mount; read what's there.
  useEffect(() => {
    if (area.current && value === undefined) setText(area.current.value)
  }, [value])

  const setRefs = (element: HTMLTextAreaElement | null) => {
    area.current = element
    if (typeof ref === 'function') ref(element)
    else if (ref) (ref as { current: HTMLTextAreaElement | null }).current = element
  }

  const apply = (edit: Edit) => {
    const element = area.current
    if (!element) return
    const expected = applyEdit(element.value, edit)
    element.focus()
    element.setSelectionRange(edit.from, edit.to)
    let done = false
    try {
      done = document.execCommand('insertText', false, edit.insert) && element.value === expected
    } catch {
      done = false
    }
    if (!done) {
      element.setRangeText(edit.insert, edit.from, edit.to, 'end')
      element.dispatchEvent(new Event('input', { bubbles: true }))
    }
    element.setSelectionRange(edit.selectStart, edit.selectEnd)
    setText(element.value)
    setCursor(edit.selectStart)
  }

  const selection = () => {
    const element = area.current!
    return [element.value, element.selectionStart, element.selectionEnd] as const
  }

  const bold = () => apply(toggleWrap(...selection(), '**', 'bold text'))
  const italic = () => apply(toggleWrap(...selection(), '*', 'italic text'))
  const list = () => apply(toggleList(...selection()))
  const align = (target: Align) => apply(setAlignment(...selection(), target))
  const startLink = () => {
    const [current, start, end] = selection()
    if (selectionIsLink(current, start, end)) {
      apply(toggleLink(current, start, end, null))
      return
    }
    setLink({ start, end })
    setUrl('')
    setLinkError(null)
  }
  const addLink = () => {
    const address = url.trim()
    if (!isSafeUrl(address)) {
      setLinkError('Use a full address starting with https://, http:// or mailto:.')
      return
    }
    const range = link!
    setLink(null)
    apply(toggleLink(area.current!.value, range.start, range.end, address))
  }
  const cancelLink = () => {
    const range = link
    setLink(null)
    setLinkError(null)
    area.current?.focus()
    if (range) area.current?.setSelectionRange(range.start, range.end)
  }

  const shortcut = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    onKeyDown?.(event)
    if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey) return
    const key = event.key.toLowerCase()
    if (key === 'b' || key === 'i' || key === 'k') {
      event.preventDefault()
      if (key === 'b') bold()
      else if (key === 'i') italic()
      else startLink()
    }
  }

  const togglePreview = () => {
    const next = !previewOpen
    setPreviewOpen(next)
    try {
      localStorage.setItem(PREVIEW_KEY, next ? 'open' : 'closed')
    } catch {
      // Not remembered; the toggle still works.
    }
  }

  // A controlled field's value is the truth (it can change from outside, e.g. a reset).
  const shown = value !== undefined ? String(value) : text
  const active = alignmentAt(shown, cursor)
  const previewId = `${id}-preview`

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-[0.8rem] font-bold text-muted">
        {label}
      </label>
      <div role="toolbar" aria-label={`Format ${label.toLowerCase()}`} aria-controls={id} className="flex flex-wrap items-center gap-1">
        <Tool label="Bold (Ctrl+B)" onClick={bold}>
          <b>B</b>
        </Tool>
        <Tool label="Italic (Ctrl+I)" onClick={italic}>
          <i className="font-serif">I</i>
        </Tool>
        <Tool label="Link (Ctrl+K)" onClick={startLink}>
          Link
        </Tool>
        <Tool label="Bulleted list" onClick={list}>
          • List
        </Tool>
        <span aria-hidden="true" className="mx-1 h-5 w-px bg-line" />
        <Tool label="Align left" pressed={active === 'left'} onClick={() => align('left')}>
          <AlignIcon lines={['w-full', 'w-3/5', 'w-4/5']} justify="items-start" />
        </Tool>
        <Tool label="Centre" pressed={active === 'center'} onClick={() => align('center')}>
          <AlignIcon lines={['w-full', 'w-3/5', 'w-4/5']} justify="items-center" />
        </Tool>
        <Tool label="Justify" pressed={active === 'justify'} onClick={() => align('justify')}>
          <AlignIcon lines={['w-full', 'w-full', 'w-full']} justify="items-start" />
        </Tool>
      </div>

      {link && (
        <div className="flex flex-wrap items-center gap-2 rounded-2xl border-[1.5px] border-line bg-canvas px-3 py-2">
          <label htmlFor={`${id}-url`} className="text-[0.8rem] font-bold text-muted">
            Link address
          </label>
          <input
            id={`${id}-url`}
            autoFocus
            type="url"
            inputMode="url"
            placeholder="https://"
            value={url}
            aria-invalid={linkError ? true : undefined}
            aria-describedby={linkError ? `${id}-url-error` : undefined}
            onChange={(event) => setUrl(event.target.value)}
            onKeyDown={(event) => {
              // Enter adds the link; it must not submit the form around the box.
              if (event.key === 'Enter') {
                event.preventDefault()
                addLink()
              } else if (event.key === 'Escape') {
                event.preventDefault()
                event.stopPropagation()
                cancelLink()
              }
            }}
            className={`${controlClasses} min-w-48 flex-1`}
          />
          <button type="button" onClick={addLink} className="h-8 rounded-full bg-accent px-3.5 text-[0.82rem] font-bold text-on-accent">
            Add link
          </button>
          <button type="button" onClick={cancelLink} className="h-8 rounded-full px-3 text-[0.82rem] font-bold text-muted hover:text-ink">
            Cancel
          </button>
          {linkError && (
            <p id={`${id}-url-error`} role="alert" className="basis-full text-xs text-danger">
              {linkError}
            </p>
          )}
        </div>
      )}

      <textarea
        ref={setRefs}
        id={id}
        rows={rows}
        value={value}
        defaultValue={defaultValue}
        aria-invalid={error ? true : undefined}
        aria-errormessage={error ? `${id}-error` : undefined}
        onChange={(event: ChangeEvent<HTMLTextAreaElement>) => {
          setText(event.target.value)
          onChange?.(event)
        }}
        onSelect={(event) => {
          setCursor(event.currentTarget.selectionStart)
          onSelect?.(event)
        }}
        onKeyDown={shortcut}
        className={`${controlClasses} h-auto rounded-2xl! py-2 leading-5 ${error ? 'border-danger' : ''} ${className}`}
        {...rest}
      />
      {hint && !error && <p className="text-xs text-muted">{hint}</p>}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs text-danger">
          {error}
        </p>
      )}

      <div className="rounded-2xl border-[1.5px] border-dashed border-line">
        <button
          type="button"
          aria-expanded={previewOpen}
          aria-controls={previewId}
          onClick={togglePreview}
          className="flex w-full items-center justify-between rounded-2xl px-3 py-1.5 text-[0.8rem] font-bold text-muted hover:text-ink"
        >
          Preview
          <span aria-hidden="true">{previewOpen ? '▾' : '▸'}</span>
        </button>
        {previewOpen && (
          <div id={previewId} role="region" aria-label={`${label} preview`} className="px-3 pt-1 pb-3">
            <FormattedPreview text={shown} />
          </div>
        )}
      </div>
    </div>
  )
}

function Tool({ label, pressed, onClick, children }: { label: string; pressed?: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      aria-pressed={pressed}
      // Keep the text area's selection: the button acts on it.
      onMouseDown={(event) => event.preventDefault()}
      onClick={onClick}
      className={`inline-flex h-8 min-w-8 items-center justify-center rounded-full border-[1.5px] px-2.5 text-[0.82rem] font-bold transition-colors ${
        pressed ? 'border-accent bg-accent text-on-accent' : 'border-line bg-surface text-accent hover:bg-accent-soft'
      }`}
    >
      {children}
    </button>
  )
}

function AlignIcon({ lines, justify }: { lines: string[]; justify: string }) {
  return (
    <span aria-hidden="true" className={`flex w-3.5 flex-col gap-[3px] ${justify}`}>
      {lines.map((width, index) => (
        <span key={index} className={`block h-[1.5px] rounded bg-current ${width}`} />
      ))}
    </span>
  )
}

