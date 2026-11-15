import { useState } from 'react'
import { useUpdateSettingsMutation } from '../../api/settingsApi'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { Input } from '../../components/ui/Input'
import { Textarea } from '../../components/ui/Textarea'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import { BOOKING_ENABLED_KEY, BOOKING_NOTE_KEY, BOOKING_STEPS_KEY } from '../../types/api'

const NOTE_MAX = 200
const MAX_STEPS = 3
const STEP_MAX = 80

/** The API's rule for the steps (blank lines don't count), so the box says what's wrong before saving. */
function stepsProblem(value: string): string | undefined {
  const steps = value.split('\n').map((line) => line.trim()).filter(Boolean)
  if (steps.length > MAX_STEPS) return `At most ${MAX_STEPS} steps, one per line.`
  if (steps.some((step) => step.length > STEP_MAX)) return `Each step up to ${STEP_MAX} characters.`
  return undefined
}

/**
 * Settings → Bookings (handover 20): whether the homepage shows the booking
 * form, and an optional line above it. Saved on its own, apart from the
 * homepage settings. Off shows the Book section exactly as before: the
 * contact buttons only.
 */
export function BookingSettings({ enabled, note, steps }: { enabled: boolean; note: string; steps: string }) {
  const [updateSettings, { isLoading }] = useUpdateSettingsMutation()
  const run = useToastedAction()
  const [baseline, setBaseline] = useState({ enabled, note, steps })
  const [on, setOn] = useState(enabled)
  const [text, setText] = useState(note)
  const [stepsText, setStepsText] = useState(steps)
  const dirty = on !== baseline.enabled || text !== baseline.note || stepsText !== baseline.steps
  const problem = stepsProblem(stepsText)

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
        <Textarea
          label="Steps"
          hint="One step per line, up to three. Shown as a numbered list beside the form. Leave empty for none."
          rows={3}
          error={problem}
          value={stepsText}
          onChange={(event) => setStepsText(event.target.value)}
        />
        <div>
          <Button
            size="sm"
            variant="primary"
            loading={isLoading}
            disabled={!dirty || problem !== undefined}
            onClick={async () => {
              const outcome = await run(
                updateSettings([
                  { key: BOOKING_ENABLED_KEY, value: on ? 'true' : 'false' },
                  { key: BOOKING_NOTE_KEY, value: text },
                  { key: BOOKING_STEPS_KEY, value: stepsText },
                ]),
                on ? 'Booking form is on.' : 'Booking form is off.',
              )
              if (outcome.ok) setBaseline({ enabled: on, note: text, steps: stepsText })
            }}
          >
            Save bookings
          </Button>
        </div>
      </div>
    </Card>
  )
}
