import { baseApi, LIST_ID, listTags } from './baseApi'
import type {
  EventDetailDto,
  EventPhotoRequest,
  EventRequest,
  EventStatus,
  EventSummaryDto,
} from '../types/api'

export interface EventFilters {
  status?: EventStatus
  published?: boolean
}

/** Anything that changes one event restates its list row and its detail page. */
function eventTags(id: string) {
  return [
    { type: 'Event', id } as const,
    { type: 'Event', id: LIST_ID } as const,
    { type: 'EventDetail', id } as const,
  ]
}

export const eventsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    listEvents: build.query<EventSummaryDto[], EventFilters>({
      query: ({ status, published }) => ({
        url: '/events',
        params: {
          ...(status ? { status } : {}),
          ...(published === undefined ? {} : { published }),
        },
      }),
      providesTags: (rows) => listTags('Event', rows),
    }),
    getEvent: build.query<EventDetailDto, string>({
      query: (id) => `/events/${id}`,
      providesTags: (_r, _e, id) => [{ type: 'EventDetail', id }],
    }),
    createEvent: build.mutation<EventDetailDto, EventRequest>({
      query: (body) => ({ url: '/events', method: 'POST', body }),
      invalidatesTags: [{ type: 'Event', id: LIST_ID }],
    }),
    updateEvent: build.mutation<EventDetailDto, { id: string; body: EventRequest }>({
      query: ({ id, body }) => ({ url: `/events/${id}`, method: 'PUT', body }),
      invalidatesTags: (_r, _e, { id }) => eventTags(id),
    }),
    setEventPublished: build.mutation<EventDetailDto, { id: string; isPublished: boolean }>({
      query: ({ id, isPublished }) => ({
        url: `/events/${id}/${isPublished ? 'publish' : 'unpublish'}`,
        method: 'PATCH',
      }),
      invalidatesTags: (_r, _e, { id }) => eventTags(id),
    }),
    /** Full replacement: the body is the event's complete photo list. */
    replaceEventPhotos: build.mutation<EventDetailDto, { id: string; photos: EventPhotoRequest[] }>({
      query: ({ id, photos }) => ({ url: `/events/${id}/photos`, method: 'PUT', body: photos }),
      // The list row shows a photo count, so it is stale too.
      invalidatesTags: (_r, _e, { id }) => eventTags(id),
    }),
    deleteEvent: build.mutation<void, string>({
      query: (id) => ({ url: `/events/${id}`, method: 'DELETE' }),
      invalidatesTags: [{ type: 'Event', id: LIST_ID }],
    }),
  }),
})

export const {
  useListEventsQuery,
  useGetEventQuery,
  useCreateEventMutation,
  useUpdateEventMutation,
  useSetEventPublishedMutation,
  useReplaceEventPhotosMutation,
  useDeleteEventMutation,
} = eventsApi
