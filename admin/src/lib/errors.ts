import type { FetchBaseQueryError } from '@reduxjs/toolkit/query'
import type { SerializedError } from '@reduxjs/toolkit'
import type { ProblemDetails } from '../types/api'

/**
 * Turns whatever RTK Query hands back into one line a cafe owner can read.
 *
 * The API answers every failure with ProblemDetails (decision 005), so the
 * useful sentence is `detail` — "Menu item 'Ube Latte' is used by 3 menu
 * version item(s). Archive it instead." `title` is the fallback, and the
 * field-level `errors` map from model validation is flattened when that is all
 * there is.
 */
export function problemMessage(
  error: FetchBaseQueryError | SerializedError | undefined,
  fallback = 'Something went wrong. Please try again.',
): string {
  if (!error) return fallback

  if (!('status' in error)) {
    return error.message ?? fallback
  }

  if (error.status === 'FETCH_ERROR') {
    return 'Could not reach the API. Is it running?'
  }

  if (error.status === 'TIMEOUT_ERROR') {
    return 'The API took too long to answer. Please try again.'
  }

  if (error.status === 'PARSING_ERROR') {
    return `The API returned something unexpected (HTTP ${error.originalStatus}).`
  }

  const problem = error.data as ProblemDetails | string | undefined

  if (typeof problem === 'string' && problem.trim() !== '') {
    return problem
  }

  if (problem && typeof problem === 'object') {
    if (problem.detail) return problem.detail

    const firstFieldError = Object.values(problem.errors ?? {})
      .flat()
      .find((message) => message.trim() !== '')
    if (firstFieldError) return firstFieldError

    if (problem.title) return problem.title
  }

  return fallback
}
