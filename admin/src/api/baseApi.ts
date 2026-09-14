import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'
import type { BaseQueryFn, FetchArgs, FetchBaseQueryError } from '@reduxjs/toolkit/query'
import { loggedOut } from '../features/auth/authSlice'
import { showToast } from '../features/toasts/toastSlice'
import type { RootState } from '../app/store'

const apiUrl = import.meta.env.VITE_API_URL
if (!apiUrl) {
  throw new Error('VITE_API_URL is not set. Copy admin/.env.example to admin/.env.')
}

const rawBaseQuery = fetchBaseQuery({
  baseUrl: `${apiUrl.replace(/\/$/, '')}/api`,
  /**
   * The token is attached once, here, instead of at every call site. A new
   * endpoint is authenticated because it goes through this base query, not
   * because someone remembered to add a header.
   */
  prepareHeaders: (headers, { getState }) => {
    const token = (getState() as RootState).auth.token
    if (token) {
      headers.set('authorization', `Bearer ${token}`)
    }
    return headers
  },
})

/**
 * One 401 rule for the whole app: the stored session is gone, so drop it and
 * say so. `RequireAuth` sees the empty token on the next render and sends the
 * user to /login.
 *
 * A 401 while signed *out* is a wrong password, not an expired session — that
 * one belongs to the login form, so it is passed through untouched.
 */
const baseQueryWithAuth: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = async (
  args,
  api,
  extraOptions,
) => {
  const wasSignedIn = (api.getState() as RootState).auth.token !== null
  const result = await rawBaseQuery(args, api, extraOptions)

  if (result.error?.status === 401 && wasSignedIn) {
    api.dispatch(loggedOut())
    api.dispatch(showToast('error', 'Your session expired. Please sign in again.'))
  }

  return result
}

/**
 * The single API slice. Feature endpoints are added with `injectEndpoints` in
 * `authApi.ts` and `menuApi.ts` so the cache, the tags and the base query stay
 * shared — one cache for the whole app, not one per feature.
 */
export const baseApi = createApi({
  reducerPath: 'api',
  baseQuery: baseQueryWithAuth,
  tagTypes: ['Me', 'Category', 'Item', 'AddOn', 'Version', 'VersionDetail'],
  endpoints: () => ({}),
})

/** The id every list query provides, so a create can invalidate "the list". */
export const LIST_ID = 'LIST' as const
