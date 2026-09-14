import { Navigate, Outlet, useLocation } from 'react-router'
import { useAppSelector } from '../../app/hooks'
import { selectIsSignedIn } from './authSlice'

/**
 * A gate in front of every route except /login. It renders nothing of its own:
 * signed in, it lets the nested routes through; signed out, it replaces the
 * navigation with /login and remembers where the owner was heading, so they
 * land back there after signing in.
 */
export function RequireAuth() {
  const isSignedIn = useAppSelector(selectIsSignedIn)
  const location = useLocation()

  if (!isSignedIn) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }

  return <Outlet />
}
