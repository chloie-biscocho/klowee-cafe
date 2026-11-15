import { useEffect, type ReactNode } from 'react'

interface ModalProps {
  open: boolean
  title: string
  onClose: () => void
  children: ReactNode
  footer?: ReactNode
  width?: string
}

/**
 * A plain dialog: a backdrop, a panel, Escape to close. No library — the app
 * needs exactly this much, and the alternative is a dependency plus its
 * conventions to learn.
 */
export function Modal({ open, title, onClose, children, footer, width = 'max-w-lg' }: ModalProps) {
  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-40 flex items-start justify-center overflow-y-auto bg-scrim p-4 sm:p-8">
      <button
        type="button"
        aria-label="Close dialog"
        className="fixed inset-0 -z-10 cursor-default"
        onClick={onClose}
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className={`w-full ${width} rounded-[18px] bg-surface shadow-lg`}
      >
        <div className="border-b-[1.5px] border-line px-5 py-3.5">
          <h2 className="font-serif text-lg text-accent">{title}</h2>
        </div>
        <div className="px-5 py-4">{children}</div>
        {footer && (
          <div className="flex justify-end gap-2 border-t-[1.5px] border-line px-5 py-3.5">{footer}</div>
        )}
      </div>
    </div>
  )
}
