import { useFieldArray, type Control, type FieldErrors, type UseFormRegister } from 'react-hook-form'
import { Button } from '../../components/ui/Button'
import { controlClasses } from '../../components/ui/Field'
import type { PackageValues } from './schemas'

interface InclusionsEditorProps {
  control: Control<PackageValues>
  register: UseFormRegister<PackageValues>
  errors: FieldErrors<PackageValues>['inclusions']
}

/** The ordered "what's included" list inside the package form. */
export function InclusionsEditor({ control, register, errors }: InclusionsEditorProps) {
  // `fields` carries a stable `id` per row, so React keeps each input's focus
  // and value attached to the right row while rows move.
  const { fields, append, remove, swap } = useFieldArray({ control, name: 'inclusions' })

  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-1.5 text-xs font-semibold text-muted">Inclusions</legend>

      {fields.length === 0 && (
        <p className="text-xs text-muted">Nothing listed yet. Add what the package comes with.</p>
      )}

      {fields.map((field, index) => {
        const error = errors?.[index]?.text?.message
        return (
          <div key={field.id} className="flex flex-col gap-1">
            <div className="flex items-center gap-1">
              <input
                aria-label={`Inclusion ${index + 1}`}
                aria-invalid={error ? true : undefined}
                placeholder="2 baristas for 3 hours"
                className={`${controlClasses} ${error ? 'border-danger' : ''}`}
                {...register(`inclusions.${index}.text`)}
              />
              <Button
                size="sm"
                variant="ghost"
                aria-label={`Move inclusion ${index + 1} up`}
                disabled={index === 0}
                onClick={() => swap(index, index - 1)}
              >
                &uarr;
              </Button>
              <Button
                size="sm"
                variant="ghost"
                aria-label={`Move inclusion ${index + 1} down`}
                disabled={index === fields.length - 1}
                onClick={() => swap(index, index + 1)}
              >
                &darr;
              </Button>
              <Button
                size="sm"
                variant="ghost"
                aria-label={`Remove inclusion ${index + 1}`}
                onClick={() => remove(index)}
              >
                &times;
              </Button>
            </div>
            {error && <p className="text-xs text-danger">{error}</p>}
          </div>
        )
      })}

      <div>
        <Button size="sm" onClick={() => append({ text: '' }, { shouldFocus: true })}>
          Add inclusion
        </Button>
      </div>
    </fieldset>
  )
}
