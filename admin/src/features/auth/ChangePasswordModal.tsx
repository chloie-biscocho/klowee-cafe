import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { useChangePasswordMutation } from '../../api/authApi'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { useToastedAction } from '../toasts/useToastedAction'

const schema = z
  .object({
    currentPassword: z.string().min(1, 'Enter your current password.'),
    newPassword: z.string().min(12, 'Use at least 12 characters.').max(200),
    confirm: z.string(),
  })
  .refine((v) => v.confirm === v.newPassword, { path: ['confirm'], message: "The two new passwords don't match." })
  .refine((v) => v.newPassword !== v.currentPassword, { path: ['newPassword'], message: 'Choose a different password.' })
type Values = z.infer<typeof schema>

/** Current, new, confirm. The API checks the current one; a wrong one is a toast, not a sign-out. */
export function ChangePasswordModal({ onClose }: { onClose: () => void }) {
  const [changePassword, { isLoading }] = useChangePasswordMutation()
  const run = useToastedAction()
  const { register, handleSubmit, formState } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '', newPassword: '', confirm: '' },
  })
  const { errors } = formState

  const submit = handleSubmit(async ({ currentPassword, newPassword }) => {
    const outcome = await run(changePassword({ currentPassword, newPassword }), 'Password changed.')
    if (outcome.ok) onClose()
  })

  return (
    <Modal
      open
      title="Change password"
      onClose={onClose}
      width="max-w-md"
      footer={
        <>
          <Button onClick={onClose} disabled={isLoading}>
            Cancel
          </Button>
          <Button variant="primary" loading={isLoading} onClick={submit}>
            Change password
          </Button>
        </>
      }
    >
      <form className="flex flex-col gap-4" onSubmit={submit}>
        <Input label="Current password" type="password" autoComplete="current-password" autoFocus error={errors.currentPassword?.message} {...register('currentPassword')} />
        <Input label="New password" type="password" autoComplete="new-password" hint="At least 12 characters." error={errors.newPassword?.message} {...register('newPassword')} />
        <Input label="Confirm new password" type="password" autoComplete="new-password" error={errors.confirm?.message} {...register('confirm')} />
        <button type="submit" hidden />
      </form>
    </Modal>
  )
}
