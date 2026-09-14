import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { z } from 'zod'
import { useLoginMutation } from '../../api/authApi'
import { useAppDispatch, useAppSelector } from '../../app/hooks'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { problemMessage } from '../../lib/errors'
import { loggedIn, selectIsSignedIn } from './authSlice'

const loginSchema = z.object({
  email: z.email('Enter a valid email address.'),
  password: z.string().min(1, 'Enter your password.'),
})

type LoginValues = z.infer<typeof loginSchema>

const DEFAULT_DESTINATION = '/menu/versions'

export function LoginPage() {
  const isSignedIn = useAppSelector(selectIsSignedIn)
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const location = useLocation()
  const [login, { isLoading, error }] = useLoginMutation()

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  // Where RequireAuth sent them from, if anywhere.
  const destination = (location.state as { from?: string } | null)?.from ?? DEFAULT_DESTINATION

  if (isSignedIn) {
    return <Navigate to={DEFAULT_DESTINATION} replace />
  }

  const onSubmit = handleSubmit(async (values) => {
    try {
      const response = await login(values).unwrap()
      dispatch(loggedIn(response))
      await navigate(destination, { replace: true })
    } catch {
      // The 401 is rendered below, straight from the API's ProblemDetails.
    }
  })

  return (
    <div className="flex min-h-screen items-center justify-center px-4">
      <form
        onSubmit={onSubmit}
        noValidate
        className="w-full max-w-sm rounded-lg border border-line bg-surface p-6"
      >
        <h1 className="text-lg font-bold tracking-tight">Klowee admin</h1>
        <p className="mt-1 mb-5 text-sm text-muted">Sign in to manage the menu.</p>

        <div className="flex flex-col gap-4">
          <Input
            label="Email"
            type="email"
            autoComplete="username"
            autoFocus
            error={errors.email?.message}
            {...register('email')}
          />
          <Input
            label="Password"
            type="password"
            autoComplete="current-password"
            error={errors.password?.message}
            {...register('password')}
          />
        </div>

        {error && (
          <p role="alert" className="mt-4 rounded-md bg-danger-soft px-3 py-2 text-sm text-danger">
            {problemMessage(error, 'Could not sign in.')}
          </p>
        )}

        <Button type="submit" variant="primary" loading={isLoading} className="mt-5 w-full">
          Sign in
        </Button>
      </form>
    </div>
  )
}
