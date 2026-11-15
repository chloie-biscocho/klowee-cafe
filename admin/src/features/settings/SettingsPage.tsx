import { useListSettingsQuery } from '../../api/settingsApi'
import { Card } from '../../components/ui/Card'
import { EmptyState } from '../../components/ui/EmptyState'
import { Spinner } from '../../components/ui/Spinner'
import { BOOKING_ENABLED_KEY, BOOKING_NOTE_KEY, TAB_NOTE_KEY } from '../../types/api'
import { BookingSettings } from './BookingSettings'
import { CustomerPayments } from './CustomerPayments'
import { SettingsForm } from './SettingsForm'
import { toSettingsValues } from './settingsForm'

export function SettingsPage() {
  const { data: settings, isLoading, isError } = useListSettingsQuery()

  if (isLoading) return <Spinner className="size-6 text-muted" />
  if (isError || !settings) {
    return (
      <Card>
        <EmptyState message="Could not load the site settings. Refresh to try again." />
      </Card>
    )
  }

  // The form reads the cache once, as its starting point; see SettingsForm.
  // Customer payments save on their own, outside the homepage form, so Enter
  // in one of their fields never submits the other.
  return (
    <>
      <SettingsForm initial={toSettingsValues(settings)} />
      <CustomerPayments note={settings.find((s) => s.key === TAB_NOTE_KEY)?.value ?? ''} />
      {/* On unless exactly "false", the API's and the website's rule (handover 20). */}
      <BookingSettings
        enabled={(settings.find((s) => s.key === BOOKING_ENABLED_KEY)?.value ?? '').trim().toLowerCase() !== 'false'}
        note={settings.find((s) => s.key === BOOKING_NOTE_KEY)?.value ?? ''}
      />
    </>
  )
}
