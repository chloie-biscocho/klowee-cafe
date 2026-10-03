import { useBlocker } from 'react-router'
import { ConfirmDialog } from './ConfirmDialog'

/**
 * Asks before leaving a screen with unsaved local edits. In-app navigation only:
 * React Router can intercept a link click, not a closed tab or a typed URL.
 * Search-param changes on the same path (a filter) are not "leaving".
 */
export function UnsavedChangesPrompt({ when, message }: { when: boolean; message: string }) {
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      when && currentLocation.pathname !== nextLocation.pathname,
  )

  return (
    <ConfirmDialog
      open={blocker.state === 'blocked'}
      title="Leave without saving?"
      message={message}
      confirmLabel="Leave"
      onCancel={() => blocker.reset?.()}
      onConfirm={() => blocker.proceed?.()}
    />
  )
}
