import { useListSettingsQuery } from '../../api/settingsApi'
import { Card } from '../../components/ui/Card'
import { EmptyState } from '../../components/ui/EmptyState'
import { Spinner } from '../../components/ui/Spinner'
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
  return <SettingsForm initial={toSettingsValues(settings)} />
}
