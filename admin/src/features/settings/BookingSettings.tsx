import { useState } from 'react'
import { useUpdateSettingsMutation } from '../../api/settingsApi'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { Input } from '../../components/ui/Input'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import { BOOKING_ENABLED_KEY, BOOKING_NOTE_KEY } from '../../types/api'

const NOTE_MAX = 200

/**
 * Settings → Bookings (handover 20): whether the homepage shows the booking
 * form, and an optional line above it. Saved on its own, apart from the
 * homepage settings. Off shows the Book section exactly as before: the
 * contact buttons only.
 */
export function BookingSettings({ enabled, note }: { enabled: boolean; note: string }) {
  const [updateSettings, { isLoading }] = useUpdateSettingsMutation()
  const run = useToastedAction()
  const [baseline, setBaseline] = useState({ enabled, note })
  const [on, setOn] = useState(enabled)
  const [text, setText] = useState(note)
  const dirty = on !== baseline.enabled || text !== baseline.note

  return (
    <Card className="mb-4">
      <CardHeader>
        <h2 className="text-[1.02rem] font-bold">Bookings</h2>
      </CardHeader>
      <div className="flex flex-col gap-4 p-4">
        <label className="flex items-center gap-3">
          <input
            type="checkbox"
            role="switch"
            checked={on}
            onChange={(event) => setOn(event.target.checked)}
            className="size-5 cursor-pointer accent-accent"
          />
          <span>
            <span className="block text-sm font-bold">Booking form on the website</span>
            <span className="block text-sm text-muted">
              {on ? 'Visitors can send a booking request from the Book section.' : 'The Book section shows your contact buttons only.'}
            </span>
          </span>
        </label>
        <Input
          label="Note above the form"
          hint="Optional, one short line, e.g. how far ahead to book."
          maxLength={NOTE_MAX}
          value={text}
          onChange={(event) => setText(event.target.value)}
        />
        <div>
          <Button
            size="sm"
            variant="primary"
            loading={isLoading}
            disabled={!dirty}
            onClick={async () => {
              const outcome = await run(
                updateSettings([
                  { key: BOOKING_ENABLED_KEY, value: on ? 'true' : 'false' },
                  { key: BOOKING_NOTE_KEY, value: text },
                ]),
                on ? 'Booking form is on.' : 'Booking form is off.',
              )
              if (outcome.ok) setBaseline({ enabled: on, note: text })
            }}
          >
            Save bookings
          </Button>
        </div>
      </div>
    </Card>
  )
}
