import { baseApi, LIST_ID, listTags } from './baseApi'
import type { AnnouncementDto, AnnouncementRequest } from '../types/api'

export const announcementsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    listAnnouncements: build.query<AnnouncementDto[], void>({
      query: () => '/announcements',
      providesTags: (rows) => listTags('Announcement', rows),
    }),
    createAnnouncement: build.mutation<AnnouncementDto, AnnouncementRequest>({
      query: (body) => ({ url: '/announcements', method: 'POST', body }),
      invalidatesTags: [{ type: 'Announcement', id: LIST_ID }],
    }),
    updateAnnouncement: build.mutation<AnnouncementDto, { id: string; body: AnnouncementRequest }>({
      query: ({ id, body }) => ({ url: `/announcements/${id}`, method: 'PUT', body }),
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Announcement', id },
        { type: 'Announcement', id: LIST_ID },
      ],
    }),
    setAnnouncementActive: build.mutation<AnnouncementDto, { id: string; isActive: boolean }>({
      query: ({ id, isActive }) => ({
        url: `/announcements/${id}/${isActive ? 'activate' : 'deactivate'}`,
        method: 'PATCH',
      }),
      invalidatesTags: (_r, _e, { id }) => [
        { type: 'Announcement', id },
        { type: 'Announcement', id: LIST_ID },
      ],
    }),
    deleteAnnouncement: build.mutation<void, string>({
      query: (id) => ({ url: `/announcements/${id}`, method: 'DELETE' }),
      invalidatesTags: [{ type: 'Announcement', id: LIST_ID }],
    }),
  }),
})

export const {
  useListAnnouncementsQuery,
  useCreateAnnouncementMutation,
  useUpdateAnnouncementMutation,
  useSetAnnouncementActiveMutation,
  useDeleteAnnouncementMutation,
} = announcementsApi
