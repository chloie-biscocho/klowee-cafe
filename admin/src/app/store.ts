import {
  combineReducers,
  configureStore,
  createListenerMiddleware,
  isAnyOf,
} from '@reduxjs/toolkit'
import { baseApi } from '../api/baseApi'
import authReducer, { loggedIn, loggedOut, writeStoredAuth } from '../features/auth/authSlice'
import toastReducer from '../features/toasts/toastSlice'

const rootReducer = combineReducers({
  [baseApi.reducerPath]: baseApi.reducer,
  auth: authReducer,
  toasts: toastReducer,
})

export type RootState = ReturnType<typeof rootReducer>

/**
 * Mirrors the auth slice into localStorage whenever it changes, so a refresh
 * keeps the owner signed in. A listener rather than a `store.subscribe`
 * callback: this runs on exactly the two actions that matter instead of on
 * every dispatch in the app.
 */
const authPersistence = createListenerMiddleware()
authPersistence.startListening({
  matcher: isAnyOf(loggedIn, loggedOut),
  effect: (_action, listenerApi) => {
    writeStoredAuth((listenerApi.getState() as RootState).auth)
  },
})

/** A fresh store. The app makes one; each test makes its own. */
export function makeStore(preloadedState?: Partial<RootState>) {
  return configureStore({
    reducer: rootReducer,
    middleware: (getDefaultMiddleware) =>
      getDefaultMiddleware().prepend(authPersistence.middleware).concat(baseApi.middleware),
    preloadedState,
  })
}

export const store = makeStore()

export type AppStore = ReturnType<typeof makeStore>
export type AppDispatch = AppStore['dispatch']
