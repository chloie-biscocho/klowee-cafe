import { useId, type ComponentPropsWithRef } from 'react'
import { Field, controlClasses } from './Field'

interface TextareaProps extends Omit<ComponentPropsWithRef<'textarea'>, 'id'> {
  label: string
  error?: string
  hint?: string
}

export function Textarea({ label, error, hint, className = '', rows = 3, ...rest }: TextareaProps) {
  const id = useId()
  return (
    <Field id={id} label={label} error={error} hint={hint}>
      <textarea
        id={id}
        rows={rows}
        aria-invalid={error ? true : undefined}
        aria-errormessage={error ? `${id}-error` : undefined}
        className={`${controlClasses} h-auto py-2 leading-5 ${error ? 'border-danger' : ''} ${className}`}
        {...rest}
      />
    </Field>
  )
}
