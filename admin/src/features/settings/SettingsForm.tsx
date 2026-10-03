import { zodResolver } from '@hookform/resolvers/zod'
import { useState, type ReactNode } from 'react'
import { Controller, useForm, useWatch, type Control } from 'react-hook-form'
import { useUpdateSettingsMutation } from '../../api/settingsApi'
import { Button } from '../../components/ui/Button'
import { Card, CardHeader } from '../../components/ui/Card'
import { ImageUpload } from '../../components/ui/ImageUpload'
import { Input } from '../../components/ui/Input'
import { Textarea } from '../../components/ui/Textarea'
import { UnsavedChangesPrompt } from '../../components/ui/UnsavedChangesPrompt'
import { useToastedAction } from '../../features/toasts/useToastedAction'
import { changedSettings, settingsSchema, toSettingsValues, type SettingsValues } from './settingsForm'

function Group({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card className="mb-4">
      <CardHeader>
        <h2 className="text-sm font-semibold">{title}</h2>
      </CardHeader>
      <div className="flex flex-col gap-4 p-4">{children}</div>
    </Card>
  )
}

function SiteImage({
  control,
  name,
  label,
}: {
  control: Control<SettingsValues>
  name: 'hero_image_url' | 'story_image_url'
  label: string
}) {
  return (
    <Controller
      control={control}
      name={name}
      render={({ field }) => (
        <ImageUpload
          label={label}
          folder="site"
          value={field.value === '' ? null : field.value}
          onChange={(url) => field.onChange(url ?? '')}
        />
      )}
    />
  )
}

/**
 * Dirty tracking: `baseline` is what the server last said, the form holds what
 * the owner sees, and "changed" is the difference between the two — which is
 * also exactly what a save sends.
 */
export function SettingsForm({ initial }: { initial: SettingsValues }) {
  const run = useToastedAction()
  const [updateSettings, { isLoading: isSaving }] = useUpdateSettingsMutation()

  const [baseline, setBaseline] = useState(initial)
  const { register, handleSubmit, control, reset, formState } = useForm<SettingsValues>({
    resolver: zodResolver(settingsSchema),
    defaultValues: initial,
  })
  const { errors } = formState

  const values = useWatch({ control }) as SettingsValues
  const isDirty = changedSettings(baseline, values).length > 0

  const onSubmit = async (submitted: SettingsValues) => {
    const saved = await run(updateSettings(changedSettings(baseline, submitted)), 'Settings saved.')
    if (!saved.ok) return
    // The response is the full set: it becomes the new baseline.
    const next = toSettingsValues(saved.data)
    setBaseline(next)
    reset(next)
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate>
      <Group title="Hero">
        <Input label="Heading" error={errors.hero_heading?.message} {...register('hero_heading')} />
        <Textarea label="Body" rows={4} error={errors.hero_body?.message} {...register('hero_body')} />
        <SiteImage control={control} name="hero_image_url" label="Hero image" />
      </Group>

      <Group title="Story">
        <Input label="Heading" error={errors.story_heading?.message} {...register('story_heading')} />
        <Textarea label="Body" rows={6} error={errors.story_body?.message} {...register('story_body')} />
        <SiteImage control={control} name="story_image_url" label="Story image" />
      </Group>

      <Group title="Ticker fallback">
        <Input
          label="Text"
          hint="Shown in the ticker when no announcement is live."
          error={errors.ticker_fallback?.message}
          {...register('ticker_fallback')}
        />
      </Group>

      <Group title="Links">
        <Input
          label="Instagram URL"
          placeholder="https://instagram.com/…"
          error={errors.instagram_url?.message}
          {...register('instagram_url')}
        />
        <Input
          label="Facebook URL"
          placeholder="https://facebook.com/…"
          error={errors.facebook_url?.message}
          {...register('facebook_url')}
        />
        <Input
          label="Contact email"
          type="email"
          error={errors.contact_email?.message}
          {...register('contact_email')}
        />
      </Group>

      <div className="sticky bottom-0 flex justify-end border-t border-line bg-canvas py-3">
        <Button type="submit" variant="primary" loading={isSaving} disabled={!isDirty}>
          Save settings
        </Button>
      </div>

      <UnsavedChangesPrompt
        when={isDirty}
        message="The site settings have unsaved changes. They will be lost."
      />
    </form>
  )
}
