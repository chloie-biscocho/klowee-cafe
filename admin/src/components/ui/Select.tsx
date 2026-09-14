import { useId, type ComponentPropsWithRef, type ReactNode } from 'react'
import { Field, controlClasses } from './Field'

interface SelectProps extends Omit<ComponentPropsWithRef<'select'>, 'id'> {
  label: string
  error?: string
  hint?: string
  children: ReactNode
}

export function Select({ label, error, hint, className = '', children, ...rest }: SelectProps) {
  const id = useId()
  return (
    <Field id={id} label={label} error={error} hint={hint}>
      <select
        id={id}
        aria-invalid={error ? true : undefined}
        aria-errormessage={error ? `${id}-error` : undefined}
        className={`${controlClasses} ${error ? 'border-danger' : ''} ${className}`}
        {...rest}
      >
        {children}
      </select>
    </Field>
  )
}
