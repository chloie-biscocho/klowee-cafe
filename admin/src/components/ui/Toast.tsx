import { useEffect } from 'react'
import type { Toast as ToastModel } from '../../features/toasts/toastSlice'

const tones = {
  success: 'border-success/30 bg-success-soft text-success',
  error: 'border-danger/30 bg-danger-soft text-danger',
} as const

const DISMISS_AFTER_MS = 6000

/**
 * One toast, responsible for its own timer. The effect here is a UI concern
 * (a countdown), not data fetching.
 */
export function Toast({ toast, onDismiss }: { toast: ToastModel; onDismiss: (id: string) => void }) {
  useEffect(() => {
    const timer = window.setTimeout(() => onDismiss(toast.id), DISMISS_AFTER_MS)
    return () => window.clearTimeout(timer)
  }, [toast.id, onDismiss])

  return (
    <div
      role="status"
      className={`pointer-events-auto flex w-80 items-start gap-3 rounded-md border px-3 py-2.5 text-sm shadow-sm ${tones[toast.tone]}`}
    >
      <span className="flex-1">{toast.message}</span>
      <button
        type="button"
        onClick={() => onDismiss(toast.id)}
        aria-label="Dismiss notification"
        className="text-base leading-none opacity-60 hover:opacity-100"
      >
        &times;
      </button>
    </div>
  )
}
