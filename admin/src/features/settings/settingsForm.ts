import { z } from 'zod'
import { SETTING_KEYS, type SettingKey, type SiteSettingDto, type SiteSettingRequest } from '../../types/api'

const MAX = 4000 // SiteSettingRequest.Value
const text = z.string().max(MAX, 'This is too long.')
const optionalUrl = z.union([z.literal(''), z.url('Enter a full URL, including https://')])

/**
 * Every value is a string, including the images (`''` = no image), because
 * that is how the API stores settings. No trimming: a body's line breaks are
 * the owner's, and an untrimmed value means "changed" means exactly what the
 * owner sees in the box.
 */
export const settingsSchema = z.object({
  hero_heading: text,
  hero_body: text,
  hero_image_url: optionalUrl,
  story_heading: text,
  story_body: text,
  story_image_url: optionalUrl,
  ticker_fallback: text,
  instagram_url: optionalUrl,
  facebook_url: optionalUrl,
  contact_email: z.union([z.literal(''), z.email('Enter an email address.')]),
}) satisfies z.ZodType<Record<SettingKey, string>>

export type SettingsValues = z.infer<typeof settingsSchema>

/** The API's list as one value per known key; a key never saved reads as empty. */
export function toSettingsValues(settings: SiteSettingDto[]): SettingsValues {
  const stored = new Map(settings.map((setting) => [setting.key, setting.value]))
  return Object.fromEntries(
    SETTING_KEYS.map((key) => [key, stored.get(key) ?? '']),
  ) as SettingsValues
}

/** The keys whose value differs from the baseline — the only ones a save sends. */
export function changedSettings(
  baseline: SettingsValues,
  values: SettingsValues,
): SiteSettingRequest[] {
  return SETTING_KEYS.filter((key) => values[key] !== baseline[key]).map((key) => ({
    key,
    value: values[key],
  }))
}
