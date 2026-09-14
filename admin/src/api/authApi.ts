import { baseApi } from './baseApi'
import type { LoginRequest, LoginResponse, UserDto } from '../types/api'

export const authApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    login: build.mutation<LoginResponse, LoginRequest>({
      query: (credentials) => ({ url: '/auth/login', method: 'POST', body: credentials }),
      // Logging in makes every cached list stale: it was fetched, if at all,
      // as somebody else or as nobody.
      invalidatesTags: ['Me', 'Category', 'Item', 'AddOn', 'Version', 'VersionDetail'],
    }),

    /**
     * Confirms the stored token is still good. Used by the app shell on load:
     * a token that expired while the tab was closed fails here, which trips the
     * 401 handler in `baseApi` and signs the user out.
     */
    getMe: build.query<UserDto, void>({
      query: () => '/auth/me',
      providesTags: ['Me'],
    }),
  }),
})

export const { useLoginMutation, useGetMeQuery } = authApi
