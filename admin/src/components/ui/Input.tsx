import { useId, type ComponentPropsWithRef } from 'react'
import { Field, controlClasses } from './Field'

interface InputProps extends Omit<ComponentPropsWithRef<'input'>, 'id'> {
  label: string
  error?: string
  hint?: string
}

export function Input({ label, error, hint, className = '', ...rest }: InputProps) {
  const id = useId()
  return (
    <Field id={id} label={label} error={error} hint={hint}>
      <input
        id={id}
        aria-invalid={error ? true : undefined}
        aria-errormessage={error ? `${id}-error` : undefined}
        className={`${controlClasses} ${error ? 'border-danger' : ''} ${className}`}
        {...rest}
      />
    </Field>
  )
}
