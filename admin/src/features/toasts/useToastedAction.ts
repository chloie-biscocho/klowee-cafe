import { useCallback } from 'react'
import type { SerializedError } from '@reduxjs/toolkit'
import type { FetchBaseQueryError } from '@reduxjs/toolkit/query'
import { useAppDispatch } from '../../app/hooks'
import { problemMessage } from '../../lib/errors'
import { showToast } from './toastSlice'

/** What RTK Query's `unwrap()` throws. */
type MutationError = FetchBaseQueryError | SerializedError

/**
 * The outcome of a mutation, as a thing you can branch on. A plain
 * `T | undefined` would not do: a 204 No Content unwraps to `undefined` too, so
 * "deleted" and "failed" would look identical.
 */
export type ActionResult<T> = { ok: true; data: T } | { ok: false }

/**
 * Runs a mutation and reports it: a success toast, or the API's ProblemDetails
 * message on a 400/404/409. Callers close their modal only when `ok`.
 *
 * Every screen goes through this, which is why a 409 reads the same everywhere.
 */
export function useToastedAction() {
  const dispatch = useAppDispatch()

  return useCallback(
    async <T>(
      action: { unwrap: () => Promise<T> },
      successMessage: string,
    ): Promise<ActionResult<T>> => {
      try {
        const data = await action.unwrap()
        dispatch(showToast('success', successMessage))
        return { ok: true, data }
      } catch (error) {
        dispatch(showToast('error', problemMessage(error as MutationError)))
        return { ok: false }
      }
    },
    [dispatch],
  )
}
