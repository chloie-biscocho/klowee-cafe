import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { Select } from '../../components/ui/Select'
import { Textarea } from '../../components/ui/Textarea'
import { todayIsoDate } from '../../lib/format'
import { MENU_CONTEXTS, MENU_CONTEXT_LABELS, type MenuVersionSummaryDto } from '../../types/api'
import { versionSchema, type VersionValues } from './schemas'

interface VersionFormModalProps {
  open: boolean
  /** `null` creates a version; anything else edits that version's details. */
  editing: { id: string; name: string; effectiveFrom: string; notes: string | null } | null
  /** Candidates for "copy items from": every version, grouped by context (an office menu often starts from the pop-up one). */
  versions: MenuVersionSummaryDto[]
  initial?: Partial<VersionValues>
  saving: boolean
  onClose: () => void
  onSubmit: (values: VersionValues) => void
}

export function VersionFormModal({
  open,
  editing,
  versions,
  initial,
  saving,
  onClose,
  onSubmit,
}: VersionFormModalProps) {
  const { register, handleSubmit, formState } = useForm<VersionValues>({
    resolver: zodResolver(versionSchema),
    defaultValues: {
      name: editing?.name ?? initial?.name ?? '',
      context: initial?.context ?? 'PopUp',
      effectiveFrom: editing?.effectiveFrom ?? initial?.effectiveFrom ?? todayIsoDate(),
      notes: editing?.notes ?? initial?.notes ?? '',
      copyFromVersionId: initial?.copyFromVersionId ?? '',
    },
  })


  return (
    <Modal
      open={open}
      title={editing ? 'Edit version details' : 'New menu version'}
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <Button variant="primary" loading={saving} onClick={handleSubmit(onSubmit)}>
            {editing ? 'Save' : 'Create draft'}
          </Button>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        <Input
          label="Name"
          autoFocus
          placeholder="April 2026 pop-up"
          error={formState.errors.name?.message}
          {...register('name')}
        />

        {/* A version's context is fixed once it exists, so editing hides it. */}
        {!editing && (
          <Select label="Context" error={formState.errors.context?.message} {...register('context')}>
            {MENU_CONTEXTS.map((value) => (
              <option key={value} value={value}>
                {MENU_CONTEXT_LABELS[value]}
              </option>
            ))}
          </Select>
        )}

        <Input
          label="Effective from"
          type="date"
          hint="The menu goes live on this date, Manila time."
          error={formState.errors.effectiveFrom?.message}
          {...register('effectiveFrom')}
        />

        {!editing && (
          <Select
            label="Copy items from"
            hint="Optional. Brings across every item, price and flag as a starting point."
            {...register('copyFromVersionId')}
          >
            <option value="">Start empty</option>
            {MENU_CONTEXTS.map((group) => {
              const candidates = versions.filter((version) => version.context === group)
              return candidates.length === 0 ? null : (
                <optgroup key={group} label={MENU_CONTEXT_LABELS[group]}>
                  {candidates.map((version) => (
                    <option key={version.id} value={version.id}>
                      {version.name} ({version.itemCount} items)
                    </option>
                  ))}
                </optgroup>
              )
            })}
          </Select>
        )}

        <Textarea label="Notes" error={formState.errors.notes?.message} {...register('notes')} />
      </div>
    </Modal>
  )
}
