import { zodResolver } from '@hookform/resolvers/zod'
import { useFieldArray, useForm, useWatch } from 'react-hook-form'
import { Button } from '../../components/ui/Button'
import { controlClasses } from '../../components/ui/Field'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { Select } from '../../components/ui/Select'
import {
  LOYALTY_CLUB_STYLES,
  LOYALTY_REWARD_TYPES,
  LOYALTY_REWARD_TYPE_LABELS,
  type AddOnDto,
  type LoyaltyClubDto,
  type MenuCategoryDto,
} from '../../types/api'
import { clubDefaults, clubSchema, type ClubInput, type ClubValues } from './schemas'

/**
 * One loyalty card: its name and look, its slots and the rewards printed on
 * them, and which menu categories feed it. Saving replaces the rewards matched
 * by slot, so editing slot 5 keeps every customer's use of it.
 */
export function LoyaltyClubForm({
  club,
  clubs,
  categories,
  addOns,
  saving,
  onClose,
  onSubmit,
}: {
  /** `null` means "new card". */
  club: LoyaltyClubDto | null
  clubs: LoyaltyClubDto[]
  categories: MenuCategoryDto[]
  addOns: AddOnDto[]
  saving: boolean
  onClose: () => void
  onSubmit: (values: ClubValues) => void
}) {
  const { register, handleSubmit, control, formState } = useForm<ClubInput, unknown, ClubValues>({
    resolver: zodResolver(clubSchema),
    defaultValues: clubDefaults(club),
  })
  const { fields, append, remove } = useFieldArray({ control, name: 'rewards' })
  const rewards = useWatch({ control, name: 'rewards' })
  const errors = formState.errors
  const otherClub = (category: MenuCategoryDto) =>
    category.loyaltyClubId && category.loyaltyClubId !== club?.id
      ? clubs.find((c) => c.id === category.loyaltyClubId)?.name
      : undefined

  return (
    <Modal
      open
      title={club ? `Edit ${club.name}` : 'New loyalty card'}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button variant="primary" loading={saving} onClick={handleSubmit(onSubmit)}>
            Save
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_140px_100px]">
          <Input label="Name" autoFocus error={errors.name?.message} {...register('name')} />
          <Select label="Style" hint="The icon and colour on the customer page." {...register('style')}>
            {LOYALTY_CLUB_STYLES.map((style) => (
              <option key={style} value={style}>
                {style}
              </option>
            ))}
          </Select>
          <Input label="Slots" type="number" min="1" error={errors.slotCount?.message} {...register('slotCount')} />
        </div>

        <label className="flex items-start gap-2 text-sm">
          <input type="checkbox" className="mt-0.5 size-4 accent-accent" {...register('isActive')} />
          <span>
            Active
            <span className="block text-xs text-muted">
              An active card earns stamps and shows on customers. The first activation dates its launch: drinks
              ordered before that day never stamp it.
            </span>
          </span>
        </label>

        <fieldset className="flex flex-col gap-2">
          <legend className="mb-1 text-xs font-semibold text-muted">Rewards</legend>
          <p className="text-xs text-muted">
            A reward on slot 5 is ready for the drink after slot 4 is filled. The drink that uses it fills its slot.
          </p>
          {fields.map((field, index) => {
            const row = errors.rewards?.[index]
            const type = rewards?.[index]?.type
            return (
              <div key={field.id} className="flex flex-col gap-1">
                <div className="grid grid-cols-[64px_minmax(0,1fr)_minmax(0,1fr)_minmax(0,1fr)_auto] items-center gap-1.5">
                  <input
                    aria-label={`Reward ${index + 1} slot`}
                    type="number"
                    min="1"
                    className={controlClasses}
                    {...register(`rewards.${index}.slot`)}
                  />
                  <select aria-label={`Reward ${index + 1} type`} className={controlClasses} {...register(`rewards.${index}.type`)}>
                    {LOYALTY_REWARD_TYPES.map((t) => (
                      <option key={t} value={t}>
                        {LOYALTY_REWARD_TYPE_LABELS[t]}
                      </option>
                    ))}
                  </select>
                  {type === 'PercentOff' ? (
                    <input
                      aria-label={`Reward ${index + 1} percent`}
                      type="number"
                      min="1"
                      max="100"
                      placeholder="%"
                      className={controlClasses}
                      {...register(`rewards.${index}.percent`)}
                    />
                  ) : type === 'FreeAddOn' ? (
                    <select aria-label={`Reward ${index + 1} add-on`} className={controlClasses} {...register(`rewards.${index}.addOnId`)}>
                      <option value="">Choose add-on</option>
                      {addOns.map((addOn) => (
                        <option key={addOn.id} value={addOn.id}>
                          {addOn.name}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <span className="px-1 text-xs text-muted">Drink free, add-ons charged</span>
                  )}
                  <input
                    aria-label={`Reward ${index + 1} label`}
                    placeholder="Label on the slot"
                    className={controlClasses}
                    {...register(`rewards.${index}.label`)}
                  />
                  <Button size="sm" variant="ghost" aria-label={`Remove reward ${index + 1}`} onClick={() => remove(index)}>
                    &times;
                  </Button>
                </div>
                {(row?.slot || row?.percent || row?.addOnId || row?.label) && (
                  <p className="text-xs text-danger">
                    {row.slot?.message ?? row.percent?.message ?? row.addOnId?.message ?? row.label?.message}
                  </p>
                )}
              </div>
            )
          })}
          <div>
            <Button
              size="sm"
              onClick={() => append({ slot: '' as unknown as number, type: 'FreeDrink', percent: '', addOnId: '', label: '' }, { shouldFocus: true })}
            >
              Add reward
            </Button>
          </div>
        </fieldset>

        <fieldset className="flex flex-col gap-1.5">
          <legend className="mb-1 text-xs font-semibold text-muted">Menu categories that feed this card</legend>
          <div className="grid gap-1 sm:grid-cols-2">
            {categories.map((category) => (
              <label key={category.id} className="flex items-center gap-2 text-sm">
                <input type="checkbox" className="size-4 accent-accent" value={category.id} {...register('categoryIds')} />
                {category.name}
                {otherClub(category) && <span className="text-xs text-muted">(now {otherClub(category)})</span>}
              </label>
            ))}
          </div>
          <p className="text-xs text-muted">
            A single drink can stamp a different card, or none, under Menu → Items (Dirty Matcha sits in Coffee but
            stamps The Matcha Club).
          </p>
        </fieldset>
      </div>
    </Modal>
  )
}
