import { baseApi } from './baseApi'
import type { SiteSettingDto, SiteSettingRequest } from '../types/api'

export const settingsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    listSettings: build.query<SiteSettingDto[], void>({
      query: () => '/settings',
      providesTags: ['Setting'],
    }),
    /** Upserts only the keys sent; the response is the full set. */
    updateSettings: build.mutation<SiteSettingDto[], SiteSettingRequest[]>({
      query: (body) => ({ url: '/settings', method: 'PUT', body }),
      invalidatesTags: ['Setting'],
    }),
  }),
})

export const { useListSettingsQuery, useUpdateSettingsMutation } = settingsApi
