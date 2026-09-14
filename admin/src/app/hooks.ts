import { useDispatch, useSelector } from 'react-redux'
import type { AppDispatch, RootState } from './store'

/**
 * Typed wrappers around react-redux's hooks. Import these, never the raw ones:
 * they know the shape of the store, so `useAppSelector` autocompletes state and
 * `useAppDispatch` accepts the app's thunks.
 */
export const useAppDispatch = useDispatch.withTypes<AppDispatch>()
export const useAppSelector = useSelector.withTypes<RootState>()
