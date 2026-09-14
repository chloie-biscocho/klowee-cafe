import { createSlice, type PayloadAction } from '@reduxjs/toolkit'
import type { LoginResponse, UserDto } from '../../types/api'

export interface AuthState {
  token: string | null
  user: UserDto | null
  /** ISO instant the token stops being accepted. */
  expiresAt: string | null
}

/**
 * One key, one blob. Splitting the token from the user would let the two drift
 * apart across tabs; this way a read is all-or-nothing.
 */
const STORAGE_KEY = 'klowee.admin.auth'

const signedOut: AuthState = { token: null, user: null, expiresAt: null }

/** Reads the persisted session, discarding anything unusable or already expired. */
export function readStoredAuth(): AuthState {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY)
    if (!raw) return signedOut

    const stored = JSON.parse(raw) as Partial<AuthState>
    if (!stored.token || !stored.user || !stored.expiresAt) return signedOut
    if (Date.parse(stored.expiresAt) <= Date.now()) return signedOut

    return { token: stored.token, user: stored.user, expiresAt: stored.expiresAt }
  } catch {
    // Private browsing, a quota error, or hand-edited JSON. Start signed out.
    return signedOut
  }
}

export function writeStoredAuth(state: AuthState): void {
  try {
    if (state.token) {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
    } else {
      window.localStorage.removeItem(STORAGE_KEY)
    }
  } catch {
    // Losing persistence is survivable; the session still works until reload.
  }
}

const authSlice = createSlice({
  name: 'auth',
  initialState: readStoredAuth(),
  reducers: {
    loggedIn(state, action: PayloadAction<LoginResponse>) {
      state.token = action.payload.accessToken
      state.user = action.payload.user
      state.expiresAt = action.payload.expiresAt
    },
    /** Logout, an expired token, and a 401 from anywhere all land here. */
    loggedOut(state) {
      state.token = null
      state.user = null
      state.expiresAt = null
    },
  },
  selectors: {
    selectToken: (state) => state.token,
    selectUser: (state) => state.user,
    selectIsSignedIn: (state) =>
      state.token !== null && state.expiresAt !== null && Date.parse(state.expiresAt) > Date.now(),
  },
})

export const { loggedIn, loggedOut } = authSlice.actions
export const { selectToken, selectUser, selectIsSignedIn } = authSlice.selectors
export default authSlice.reducer
