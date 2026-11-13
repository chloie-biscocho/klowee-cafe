import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useAdjustStampsMutation } from '../../../api/salesApi'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { Modal } from '../../../components/ui/Modal'
import { Select } from '../../../components/ui/Select'
import type { CustomerClubDto } from '../../../types/api'
import { useToastedAction } from '../../toasts/useToastedAction'
import { adjustSchema, blankToNull, type AdjustValues } from '../schemas'

/** Adds or removes stamps on one card, with a reason (an Adjustment on that club's ledger). */
export function AdjustStampsModal({
  customer,
  clubs,
  clubId,
  onClose,
}: {
  customer: { id: string; name: string }
  /** The active cards; the owner chooses which one. */
  clubs: CustomerClubDto[]
  /** The card to start on. */
  clubId?: string
  onClose: () => void
}) {
  const [adjust, { isLoading }] = useAdjustStampsMutation()
  const run = useToastedAction()
  const { register, handleSubmit, formState } = useForm<AdjustValues>({
    resolver: zodResolver(adjustSchema),
    defaultValues: { clubId: clubId ?? clubs[0]?.clubId ?? '', delta: 1, notes: '' },
  })

  const submit = handleSubmit(async (values) => {
    const outcome = await run(
      adjust({ id: customer.id, body: { clubId: values.clubId, delta: values.delta, notes: blankToNull(values.notes) } }),
      'Stamps adjusted.',
    )
    if (outcome.ok) onClose()
  })

  return (
    <Modal
      open
      title={`Adjust ${customer.name}'s stamps`}
      onClose={onClose}
      width="max-w-md"
      footer={
        <>
          <Button onClick={onClose} disabled={isLoading}>
            Cancel
          </Button>
          <Button variant="primary" loading={isLoading} onClick={submit}>
            Save
          </Button>
        </>
      }
    >
      <form className="flex flex-col gap-4" onSubmit={submit}>
        <Select label="Card" error={formState.errors.clubId?.message} {...register('clubId')}>
          {clubs.map((club) => (
            <option key={club.clubId} value={club.clubId}>
              {club.name} ({club.position} of {club.slotCount})
            </option>
          ))}
        </Select>
        <Input
          label="Stamps"
          type="number"
          step="1"
          autoFocus
          hint="Positive to add, negative to remove."
          error={formState.errors.delta?.message}
          {...register('delta')}
        />
        <Input label="Reason" placeholder="Spilled drink, re-made" error={formState.errors.notes?.message} {...register('notes')} />
        <button type="submit" hidden />
      </form>
    </Modal>
  )
}
