import type { BookingStatus } from '../../types/api'

/** New stands out; Contacted is in progress; Confirmed is good news; Declined is done with. */
export const BOOKING_STATUS_TONES: Record<BookingStatus, 'accent' | 'warning' | 'success' | 'neutral'> = {
  New: 'accent',
  Contacted: 'warning',
  Confirmed: 'success',
  Declined: 'neutral',
}

/** What each status means, for the buttons' titles and the guide. */
export const BOOKING_STATUS_HINTS: Record<BookingStatus, string> = {
  New: 'Not answered yet',
  Contacted: "You've replied and are talking it over",
  Confirmed: "It's happening",
  Declined: "It isn't happening",
}
