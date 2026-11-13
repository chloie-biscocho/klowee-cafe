import { baseApi, LIST_ID, listTags } from './baseApi'
import type { LoyaltyClubDto, LoyaltyClubRequest } from '../types/api'

/**
 * The loyalty cards (clubs, decision 020). A club's rewards and active state
 * show on every customer's card, and its category checklist rewrites the
 * categories (and so which club each version item stamps); a change refreshes
 * all of those.
 */
const CLUB_CHANGED = [{ type: 'LoyaltyClub', id: LIST_ID }, 'Customer', 'Category', 'VersionDetail'] as const

export const loyaltyApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    listClubs: build.query<LoyaltyClubDto[], void>({
      query: () => '/loyalty/clubs',
      providesTags: (rows) => listTags('LoyaltyClub', rows),
    }),
    createClub: build.mutation<LoyaltyClubDto, LoyaltyClubRequest>({
      query: (body) => ({ url: '/loyalty/clubs', method: 'POST', body }),
      invalidatesTags: [...CLUB_CHANGED],
    }),
    updateClub: build.mutation<LoyaltyClubDto, { id: string; body: LoyaltyClubRequest }>({
      query: ({ id, body }) => ({ url: `/loyalty/clubs/${id}`, method: 'PUT', body }),
      invalidatesTags: (_r, _e, { id }) => [{ type: 'LoyaltyClub', id }, ...CLUB_CHANGED],
    }),
    deleteClub: build.mutation<void, string>({
      query: (id) => ({ url: `/loyalty/clubs/${id}`, method: 'DELETE' }),
      invalidatesTags: [...CLUB_CHANGED, 'Item'],
    }),
  }),
})

export const { useListClubsQuery, useCreateClubMutation, useUpdateClubMutation, useDeleteClubMutation } = loyaltyApi
