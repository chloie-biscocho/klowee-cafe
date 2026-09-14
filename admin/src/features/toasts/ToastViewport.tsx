import { useCallback } from 'react'
import { useAppDispatch, useAppSelector } from '../../app/hooks'
import { Toast } from '../../components/ui/Toast'
import { dismissToast, selectToasts } from './toastSlice'

/** Bottom-right stack. Mounted once, above the router, so login has toasts too. */
export function ToastViewport() {
  const toasts = useAppSelector(selectToasts)
  const dispatch = useAppDispatch()

  const onDismiss = useCallback((id: string) => dispatch(dismissToast(id)), [dispatch])

  if (toasts.length === 0) return null

  return (
    <div className="pointer-events-none fixed right-4 bottom-4 z-50 flex flex-col gap-2">
      {toasts.map((toast) => (
        <Toast key={toast.id} toast={toast} onDismiss={onDismiss} />
      ))}
    </div>
  )
}
