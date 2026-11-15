import { baseApi, LIST_ID, listTags } from './baseApi'
import type { BookingDto, BookingStatus, BookingSummaryDto, PagedResult, UpdateBookingRequest } from '../types/api'

export interface BookingFilters {
  status?: BookingStatus
  page: number
  pageSize: number
}

/**
 * Booking requests from the homepage (handover 20). A status change or a
 * delete also refreshes the summary ('Report'), which drives the Dashboard's
 * count and the sidebar badge.
 */
export const bookingsApi = baseApi.injectEndpoints({
  endpoints: (build) => ({
    listBookings: build.query<PagedResult<BookingSummaryDto>, BookingFilters>({
      query: ({ status, page, pageSize }) => ({ url: '/bookings', params: { ...(status ? { status } : {}), page, pageSize } }),
      providesTags: (result) => listTags('Booking', result?.items),
    }),
    getBooking: build.query<BookingDto, string>({
      query: (id) => `/bookings/${id}`,
      providesTags: (_r, _e, id) => [{ type: 'Booking', id }],
    }),
    updateBooking: build.mutation<BookingDto, { id: string; body: UpdateBookingRequest }>({
      query: ({ id, body }) => ({ url: `/bookings/${id}`, method: 'PATCH', body }),
      invalidatesTags: (_r, _e, { id }) => [{ type: 'Booking', id }, { type: 'Booking', id: LIST_ID }, 'Report'],
    }),
    deleteBooking: build.mutation<void, string>({
      query: (id) => ({ url: `/bookings/${id}`, method: 'DELETE' }),
      invalidatesTags: [{ type: 'Booking', id: LIST_ID }, 'Report'],
    }),
  }),
})

export const { useListBookingsQuery, useGetBookingQuery, useUpdateBookingMutation, useDeleteBookingMutation } = bookingsApi
